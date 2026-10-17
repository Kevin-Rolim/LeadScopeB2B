using LeadScopeB2B.Data;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class IntegracaoRepository(SqlLeadScopeB2B sql)
{
    private static DadosColetados Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("id"),
        Campo = r.Text("campo")!,
        Valor = r.Text("valor")!,
        ValorNormalizado = r.Text("valor_normalizado"),
        StatusValidacao = r.Text("status_validacao") ?? "PENDENTE",
        NivelConfianca = r.Decimal("nivel_confianca"),
        FonteDadosID = r.Int("fonte_dados_id"),
        DataColeta = r.Date("data_coleta")!.Value,
        EvidenciaUrl = r.Text("evidencia_url"),
        PessoaId = r.IsDBNull(r.GetOrdinal("pessoa_id")) ? null : r.Int("pessoa_id"),
        EmpresaId = r.IsDBNull(r.GetOrdinal("empresa_id")) ? null : r.Int("empresa_id")
    };
    public Task<List<DadosColetados>> ListarAsync(string? status) => sql.ConsultarAsync("SELECT TOP (200) * FROM dbo.Dados_coletados WHERE @status IS NULL OR status_validacao=@status ORDER BY data_coleta DESC,id DESC", Mapear, Text("@status", status, 30));
    public async Task<DadosColetados?> ObterAsync(int id) => (await sql.ConsultarAsync("SELECT * FROM dbo.Dados_coletados WHERE id=@id", Mapear, Int("@id", id))).SingleOrDefault();
    private static SqlParameter[] Parametros(DadosColetados d) => [Int("@pessoa", d.PessoaId), Int("@empresa", d.EmpresaId), Int("@fonte", d.FonteDadosID), Text("@campo", d.Campo, 100), Text("@valor", d.Valor), Text("@normalizado", d.ValorNormalizado), Decimal("@confianca", d.NivelConfianca), Text("@evidencia", d.EvidenciaUrl, 1000)];
    public async Task<int> AdicionarAsync(DadosColetados dado, int usuarioId) => await sql.TransacaoAsync(async (conn, tx) =>
    {
        await ValidarDestinoAsync(conn, tx, dado);
        var fontes = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT id FROM dbo.Fontes_dados WITH (UPDLOCK,HOLDLOCK) WHERE id=@id AND ativo=1 AND data_hora_del IS NULL", r => r.GetInt32(0), Int("@id", dado.FonteDadosID));
        if (fontes.Count == 0) throw new ConflitoException("Fonte inativa.");
        var duplicate = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT id FROM dbo.Dados_coletados WITH (UPDLOCK,HOLDLOCK) WHERE fonte_dados_id=@fonte AND campo=@campo AND valor_normalizado=@normalizado AND status_validacao='PENDENTE' AND ((pessoa_id=@pessoa AND @empresa IS NULL) OR (empresa_id=@empresa AND @pessoa IS NULL))", r => r.GetInt32(0), Parametros(dado));
        if (duplicate.Count > 0) return duplicate[0];
        var id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.Dados_coletados (campo,valor,valor_normalizado,nivel_confianca,status_validacao,evidencia_url,fonte_dados_id,pessoa_id,empresa_id) OUTPUT INSERTED.id VALUES (@campo,@valor,@normalizado,@confianca,'PENDENTE',@evidencia,@fonte,@pessoa,@empresa)", r => r.GetInt32(0), Parametros(dado))).Single();
        await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "DADO_RECEBIDO", $"Evidência {id} recebida da fonte {dado.FonteDadosID}.");
        return id;
    });
    private static async Task ValidarDestinoAsync(SqlConnection conn, SqlTransaction tx, DadosColetados dado)
    {
        var query = dado.PessoaId.HasValue
            ? "SELECT id FROM dbo.Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE id=@id AND data_del IS NULL AND data_bloqueio IS NULL"
            : "SELECT id FROM dbo.Empresas WITH (UPDLOCK,HOLDLOCK) WHERE id=@id AND status_validacao NOT IN ('REMOVIDA','INATIVA')";
        var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, query, r => r.GetInt32(0), Int("@id", dado.PessoaId ?? dado.EmpresaId));
        if (valid.Count == 0) throw new ConflitoException("Destino inexistente, bloqueado ou removido.");
    }
    private static string Coluna(DadosColetados d) => (d.PessoaId.HasValue, d.Campo) switch
    {
        (true, "nome") => "nome",
        (true, "email") => "email_profissional",
        (true, "telefone") => "telefone_comercial",
        (true, "linkedin") => "url_perfil_profissional",
        (true, "origem") => "origem",
        (false, "razao_social") => "razao_social",
        (false, "nome_fantasia") => "nome_fantasia",
        (false, "cnpj") => "cnpj",
        (false, "site") => "site",
        (false, "segmento") => "segmento",
        (false, "porte") => "porte",
        (false, "cidade") => "cidade",
        (false, "estado") => "estado",
        (false, "email") => "email_institucional",
        (false, "telefone") => "telefone_comercial",
        _ => throw new System.ComponentModel.DataAnnotations.ValidationException("Campo não permitido.")
    };
    public async Task RevisarAsync(DadosColetados dado, string valor, bool aprovar, int usuarioId)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var estados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT status_validacao FROM dbo.Dados_coletados WITH (UPDLOCK,HOLDLOCK) WHERE id=@id", r => r.GetString(0), Int("@id", dado.Id));
            if (estados.Count == 0) throw new KeyNotFoundException("Evidência não encontrada.");
            if (estados[0] != "PENDENTE") throw new ConflitoException("Esta evidência já foi revisada.");
            if (aprovar)
            {
                await ValidarDestinoAsync(conn, tx, dado);
                if (dado.PessoaId.HasValue && dado.Campo is "email" or "linkedin")
                {
                    var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, $"SELECT id FROM dbo.Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE id<>@id AND LOWER({Coluna(dado)})=LOWER(@valor)", r => r.GetInt32(0), Int("@id", dado.PessoaId), Text("@valor", valor));
                    if (duplicados.Count > 0) throw new ConflitoException("O dado aprovado geraria uma pessoa duplicada.");
                }
                // Tabela e coluna vêm exclusivamente da lista fixa acima; valores continuam parametrizados.
                var table = dado.PessoaId.HasValue ? "Pessoas" : "Empresas";
                var timestamp = dado.PessoaId.HasValue ? "data_verificacao" : "data_atualizacao";
                var complemento = dado.Campo == "cnpj" ? ",cnpj_raiz=LEFT(@valor,8)" : "";
                await SqlLeadScopeB2B.ExecutarAsync(conn, tx, $"UPDATE dbo.{table} SET {Coluna(dado)}=@valor{complemento},{timestamp}=SYSUTCDATETIME() WHERE id=@id", Text("@valor", valor), Int("@id", dado.PessoaId ?? dado.EmpresaId));
            }
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Dados_coletados SET valor_normalizado=@valor,status_validacao=@status WHERE id=@id", Int("@id", dado.Id), Text("@valor", valor), Text("@status", aprovar ? "VALIDADO" : "REJEITADO", 30));
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "REVISAO_DADO", $"Evidência {dado.Id}: {(aprovar ? "VALIDADO" : "REJEITADO")}.");
            return true;
        });
    }
}
