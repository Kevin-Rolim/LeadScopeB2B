using System.Data;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Services;
using LeadScopeB2B.ViewModels.Empresas;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public partial class IntegracaoRepository
{
    public async Task<List<EvidenciaEmpresa>> EvidenciasEmpresaAsync(int empresaId, CancellationToken ct)
    {
        await using var conn=sql.Conn(); await conn.OpenAsync(ct);
        return await ConsultarColetaAsync(conn,null,"""
            SELECT d.Id,d.Campo,d.Valor,f.Nome,
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Logs l WHERE l.TipoEvento='REVISAO_DADO' AND l.DescricaoEvento=CONCAT('Evidência ',d.Id,': APROVADA')) THEN 'Aprovada'
                WHEN EXISTS (SELECT 1 FROM dbo.Logs l WHERE l.TipoEvento='REVISAO_DADO' AND l.DescricaoEvento=CONCAT('Evidência ',d.Id,': REJEITADA')) THEN 'Rejeitada'
                WHEN d.Campo='Cnpj' THEN 'Referência de identidade' ELSE 'Pendente' END AS Estado
            FROM dbo.DadosColetados d JOIN dbo.FonteDados f ON f.Id=d.FonteDadosId
            WHERE d.EmpresaId=@id ORDER BY d.Id DESC
            """,r=>new EvidenciaEmpresa(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),ct,Int("@id",empresaId));
    }
    public virtual async Task<int> PrepararFonteIntegracaoAsync(string nome, string url, CancellationToken ct)
    {
        if (url.Length > 255) throw new ConflitoException("A origem excede o tamanho do cadastro de fontes. Selecione uma fonte nas opções avançadas.");
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var found = await ConsultarColetaAsync(conn, tx, "SELECT Id,DataHoraExclusao FROM dbo.FonteDados WITH (UPDLOCK,HOLDLOCK) WHERE Nome=@nome AND Url=@url", r => (Id: r.GetInt32(0), Ativa: r.IsDBNull(1)), ct, Text("@nome", nome, 100), Text("@url", url, 255));
        if (found.Count > 0)
        {
            var ativa = found.FirstOrDefault(f=>f.Ativa);
            if (!ativa.Ativa) throw new ConflitoException("A fonte desta API foi desativada. Reative-a nas opções avançadas antes de coletar evidências.");
            await tx.CommitAsync(ct); return ativa.Id;
        }
        var inserted = await ConsultarColetaAsync(conn, tx, "INSERT INTO dbo.FonteDados (Nome,Tipo,Url) OUTPUT INSERTED.Id VALUES (@nome,'API',@url)", r=>r.GetInt32(0), ct, Text("@nome", nome, 100), Text("@url", url, 255));
        await tx.CommitAsync(ct); return inserted.Single();
    }
    public virtual async Task<bool> UsuarioPodeIntegrarAsync(int usuarioId, CancellationToken ct)
    {
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        return (await ConsultarColetaAsync(conn, null, """
            SELECT u.Id FROM dbo.Usuarios u JOIN dbo.PerfisAcesso p ON p.Id=u.PerfilAcessoId
            JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId WHERE u.Id=@usuario AND c.Nome='Admin'
            """, r => r.GetInt32(0), ct, Int("@usuario", usuarioId))).Count == 1;
    }
    public virtual async Task<bool> FonteIntegracaoAtivaAsync(int fonteId, CancellationToken ct)
    {
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        return (await ConsultarColetaAsync(conn, null, "SELECT Id FROM dbo.FonteDados WHERE Id=@id AND DataHoraExclusao IS NULL", r => r.GetInt32(0), ct, Int("@id", fonteId))).Count == 1;
    }
    public virtual async Task<DestinoDadosApi?> ObterDestinoIntegracaoAsync(int? empresaId, int? pessoaId, CancellationToken ct)
    {
        if (empresaId.HasValue == pessoaId.HasValue) return null;
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        var pessoa = pessoaId.HasValue;
        var query = pessoa ? "SELECT * FROM dbo.Pessoas WHERE Id=@id AND DataExclusao IS NULL AND DataBloqueio IS NULL" : "SELECT * FROM dbo.Empresas WHERE Id=@id AND ISNULL(Status,'') NOT IN ('REMOVIDA','INATIVA')";
        var rows = await ConsultarColetaAsync(conn, null, query, r => MapearDestino(r, pessoa), ct, Int("@id", pessoaId ?? empresaId));
        return rows.SingleOrDefault();
    }
    private static DestinoDadosApi MapearDestino(SqlDataReader r, bool pessoa)
    {
        var result = new DestinoDadosApi { EmpresaId = pessoa ? null : r.Int("Id"), PessoaId = pessoa ? r.Int("Id") : null };
        foreach (var name in (pessoa ? new[] { "Id", "Nome", "Email", "Telefone", "UrlLinkedIn", "Origem" } : new[] { "Id", "RazaoSocial", "NomeFantasia", "Cnpj", "UrlSite", "Segmento", "Porte", "Cidade", "Estado", "Email", "Telefone" }))
            result.Campos[(pessoa ? "Pessoa." : "Empresa.") + name] = name == "Id" ? r.Int("Id").ToString(System.Globalization.CultureInfo.InvariantCulture) : r.Text(name);
        return result;
    }
    public virtual async Task<List<DestinoDadosApi>> CandidatosIntegracaoAsync(int fonteId, bool pessoas, CancellationToken ct, bool somenteSemEvidencias = true)
    {
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        var table = pessoas ? "Pessoas" : "Empresas";
        var idColumn = pessoas ? "PessoaId" : "EmpresaId";
        var condition = pessoas ? "e.DataExclusao IS NULL AND e.DataBloqueio IS NULL" : "ISNULL(e.Status,'') NOT IN ('REMOVIDA','INATIVA')";
        return await ConsultarColetaAsync(conn, null, $"""
            SELECT e.* FROM dbo.{table} e WHERE {condition}
            AND (@sem=0 OR NOT EXISTS (SELECT 1 FROM dbo.DadosColetados d WHERE d.{idColumn}=e.Id AND d.FonteDadosId=@fonte))
            ORDER BY e.Id
            """, r => MapearDestino(r, pessoas), ct, Int("@fonte", fonteId), Bit("@sem", somenteSemEvidencias));
    }
    public virtual async Task<int> SalvarEvidenciasIntegracaoAsync(DestinoDadosApi destino, IReadOnlyDictionary<string, string> campos,
        int fonteId, int usuarioId, CancellationToken ct)
    {
        await using var conn = sql.Conn(); await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var pessoa = destino.PessoaId.HasValue;
        if (pessoa == destino.EmpresaId.HasValue) throw new ConflitoException("Informe exatamente um destino existente.");
        var table = pessoa ? "Pessoas" : "Empresas";
        var condition = pessoa ? "DataExclusao IS NULL AND DataBloqueio IS NULL" : "ISNULL(Status,'') NOT IN ('REMOVIDA','INATIVA')";
        var atuais = await ConsultarColetaAsync(conn, tx, $"SELECT * FROM dbo.{table} WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND {condition}", r => MapearDestino(r, pessoa), ct, Int("@id", destino.PessoaId ?? destino.EmpresaId));
        if (atuais.Count == 0) throw new ConflitoException("Destino indisponível.");
        if (!pessoa && atuais[0].Campos.GetValueOrDefault("Empresa.Cnpj") != destino.Campos.GetValueOrDefault("Empresa.Cnpj")) throw new ConflitoException("CNPJ alterado durante a consulta; resposta descartada.");
        var fontes = await ConsultarColetaAsync(conn, tx, "SELECT Id FROM dbo.FonteDados WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND DataHoraExclusao IS NULL", r => r.GetInt32(0), ct, Int("@id", fonteId));
        if (fontes.Count != 1) throw new ConflitoException("Fonte indisponível.");
        var count = 0;
        foreach (var (logical, raw) in campos)
        {
            if (!atuais[0].Campos.ContainsKey(logical) || logical.EndsWith(".Id", StringComparison.Ordinal)) throw new ConflitoException("Mapeamento incompatível com o destino existente.");
            var field = logical.Split('.')[1];
            var valor = IntegracaoService.Normalizar(pessoa, field, raw);
            var inseridos = await ConsultarColetaAsync(conn, tx, """
                INSERT INTO dbo.DadosColetados (Campo,Valor,FonteDadosId,PessoaId,EmpresaId)
                OUTPUT INSERTED.Id
                SELECT @campo,@valor,@fonte,@pessoa,@empresa WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.DadosColetados WITH (UPDLOCK,HOLDLOCK) WHERE FonteDadosId=@fonte AND Campo=@campo AND Valor=@valor
                    AND ((EmpresaId=@empresa AND PessoaId IS NULL) OR (PessoaId=@pessoa AND EmpresaId IS NULL)))
                """, r => r.GetInt32(0), ct, Text("@campo", field, 100), Text("@valor", valor), Int("@fonte", fonteId), Int("@pessoa", destino.PessoaId), Int("@empresa", destino.EmpresaId));
            foreach (var id in inseridos)
            {
                var igual = string.Equals(atuais[0].Campos[logical]?.Trim(), valor, StringComparison.OrdinalIgnoreCase);
                await ConsultarColetaAsync(conn, tx, """
                    INSERT INTO dbo.Logs (TipoEvento,DescricaoEvento,UsuarioId) VALUES ('DADO_RECEBIDO',@descricao,@usuario)
                    """, r => r.GetInt32(0), ct, Text("@descricao", $"Evidência {id}; fonte {fonteId}; campo {field}; {(igual ? "CONCORDANTE" : "DIVERGENTE")}; revisão humana necessária."), Int("@usuario", usuarioId));
                count++;
            }
        }
        await tx.CommitAsync(ct);
        return count;
    }
}
