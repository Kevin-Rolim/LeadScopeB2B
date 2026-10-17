using LeadScopeB2B.Data;
using LeadScopeB2B.Services;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public record LeadExportado(int PessoaId, int EmpresaId, string Pessoa, string Empresa, string? Email, string? Telefone, string? Cargo);
public class ExportacaoRepository(SqlLeadScopeB2B sql)
{
    public async Task<List<LeadExportado>> ExportarAsync(IReadOnlyList<(int Pessoa, int Empresa)> pares, int usuarioId) => await sql.TransacaoAsync(async (conn, tx) =>
    {
        var result = new List<LeadExportado>();
        foreach (var (pessoa, empresa) in pares)
        {
            var rows = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, """
                SELECT p.id AS pessoa_id,e.id AS empresa_id,p.nome,e.razao_social,p.email_profissional,p.telefone_comercial,l.cargo
                FROM dbo.Pessoas_Empresas l WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Pessoas p ON p.id=l.pessoa_id JOIN dbo.Empresas e ON e.id=l.empresa_id
                WHERE l.pessoa_id=@pessoa AND l.empresa_id=@empresa AND l.status_lead='QUALIFICADO' AND l.status_vinculo='ATIVO'
                AND p.data_del IS NULL AND p.data_bloqueio IS NULL AND p.status_validacao='VALIDADO' AND e.status_validacao='VALIDADA'
                """, r => new LeadExportado(r.Int("pessoa_id"), r.Int("empresa_id"), r.Text("nome")!, r.Text("razao_social")!, r.Text("email_profissional"), r.Text("telefone_comercial"), r.Text("cargo")), Int("@pessoa", pessoa), Int("@empresa", empresa));
            if (rows.Count != 1) throw new ConflitoException("Exporte somente leads qualificados com vínculo ativo, pessoa validada e desbloqueada e empresa validada.");
            result.Add(rows[0]);
        }
        var id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.Exportacoes (qntd_itens,formato,usuario_id) OUTPUT INSERTED.id VALUES (@quantidade,'CSV',@usuario)", r => r.GetInt32(0), Int("@quantidade", result.Count), Int("@usuario", usuarioId))).Single();
        foreach (var (pessoa, empresa) in pares)
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Exportacoes_Pessoas_Empresas (exportacao_id,pessoa_id,empresa_id) VALUES (@id,@pessoa,@empresa)", Int("@id", id), Int("@pessoa", pessoa), Int("@empresa", empresa));
        await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "EXPORTACAO", $"Exportação {id}: {result.Count} leads em CSV.");
        return result;
    });
}
