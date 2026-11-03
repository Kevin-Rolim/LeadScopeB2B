using System.Data;
using LeadScopeB2B.Data;
using LeadScopeB2B.Integration;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public partial class IntegracaoRepository
{
    private static async Task<List<T>> ConsultarColetaAsync<T>(SqlConnection conn, SqlTransaction? tx, string query,
        Func<SqlDataReader, T> map, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var cmd = new SqlCommand(query, conn, tx);
        cmd.Parameters.AddRange(parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(map(reader));
        return result;
    }

    private static string ColunaRevisao(string campo, bool pessoa = false) => (pessoa, campo) switch
    {
        (false, "RazaoSocial" or "NomeFantasia" or "Porte" or "Cidade" or "Estado" or "UrlSite" or "Segmento" or "Email" or "Telefone") => campo,
        (true, "Nome" or "Email" or "Telefone" or "UrlLinkedIn" or "Origem") => campo,
        _ => throw new ConflitoException("Campo sem suporte de aprovação; CNPJ permanece referência de identidade.")
    };

    public async Task<RevisaoColeta?> ObterRevisaoColetaAsync(int id)
    {
        var dado = await ObterAsync(id);
        if (dado is null || dado.Campo == "Cnpj") return null;
        var coluna = ColunaRevisao(dado.Campo, dado.PessoaId.HasValue);
        var tabela = dado.PessoaId.HasValue ? "Pessoas" : "Empresas";
        var atuais = await sql.ConsultarAsync($"SELECT {coluna} AS Valor FROM dbo.{tabela} WHERE Id=@id", r => r.Text("Valor"), Int("@id", dado.PessoaId ?? dado.EmpresaId));
        if (atuais.Count == 0) return null;
        var revisoes = await sql.ConsultarAsync("SELECT Id FROM dbo.Logs WHERE TipoEvento='REVISAO_DADO' AND DescricaoEvento IN (@aprovada,@rejeitada)", r => r.GetInt32(0),
            Text("@aprovada", $"Evidência {id}: APROVADA"), Text("@rejeitada", $"Evidência {id}: REJEITADA"));
        return new(dado, atuais[0], revisoes.Count > 0);
    }

    public async Task RevisarColetaAsync(int id, string? valorEsperado, bool aprovar, int usuarioId)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var dados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, $"SELECT {Colunas} FROM dbo.DadosColetados WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id", Mapear, Int("@id", id));
            var dado = dados.SingleOrDefault() ?? throw new KeyNotFoundException("Evidência não encontrada.");
            var pessoa = dado.PessoaId.HasValue;
            var coluna = ColunaRevisao(dado.Campo, pessoa);
            var tabela = pessoa ? "Pessoas" : "Empresas";
            var ativo = pessoa ? "e.DataExclusao IS NULL AND e.DataBloqueio IS NULL" : "ISNULL(e.Status,'') NOT IN ('REMOVIDA','INATIVA')";
            var revisadas = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.Logs WITH (UPDLOCK,HOLDLOCK) WHERE TipoEvento='REVISAO_DADO' AND DescricaoEvento IN (@aprovada,@rejeitada)", r => r.GetInt32(0),
                Text("@aprovada", $"Evidência {id}: APROVADA"), Text("@rejeitada", $"Evidência {id}: REJEITADA"));
            if (revisadas.Count > 0) throw new ConflitoException("Esta evidência já foi revisada.");
            var atuais = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, $"""
                SELECT e.{coluna} AS Valor FROM dbo.{tabela} e WITH (UPDLOCK,HOLDLOCK)
                JOIN dbo.FonteDados f WITH (UPDLOCK,HOLDLOCK) ON f.Id=@fonte
                WHERE e.Id=@empresa AND {ativo}
                AND f.DataHoraExclusao IS NULL
                """, r => r.Text("Valor"), Int("@empresa", dado.PessoaId ?? dado.EmpresaId), Int("@fonte", dado.FonteDadosId));
            if (atuais.Count == 0) throw new ConflitoException("Empresa ou fonte inativa.");
            if (!string.Equals(atuais[0] ?? "", valorEsperado ?? "", StringComparison.Ordinal))
                throw new ConflitoException("O cadastro mudou. Reabra a comparação antes de decidir.");
            if (aprovar)
            {
                if (!pessoa)
                {
                var identidades = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                    "SELECT Valor FROM dbo.DadosColetados WITH (UPDLOCK,HOLDLOCK) WHERE EmpresaId=@empresa AND FonteDadosId=@fonte AND Campo='Cnpj'",
                    r => r.GetString(0), Int("@empresa", dado.EmpresaId), Int("@fonte", dado.FonteDadosId));
                var cnpjsAtuais = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                    "SELECT Cnpj FROM dbo.Empresas WHERE Id=@empresa", r => r.Text("Cnpj"), Int("@empresa", dado.EmpresaId));
                if (identidades.Any(cnpj => CadastroRules.Cnpj(cnpj) != CadastroRules.Cnpj(cnpjsAtuais.Single())))
                    throw new ConflitoException("O CNPJ do cadastro mudou desde a coleta. Esta evidência não pode ser aplicada.");
                }
                var valor = IntegracaoService.Normalizar(pessoa, dado.Campo, dado.Valor);
                await SqlLeadScopeB2B.ExecutarAsync(conn, tx, $"UPDATE dbo.{tabela} SET {coluna}=@valor{(pessoa ? "" : ",DataAtualizacao=SYSUTCDATETIME()")} WHERE Id=@empresa",
                    Text("@valor", valor), Int("@empresa", dado.PessoaId ?? dado.EmpresaId));
            }
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "REVISAO_DADO", $"Evidência {id}: {(aprovar ? "APROVADA" : "REJEITADA")}");
            return true;
        });
    }

}
