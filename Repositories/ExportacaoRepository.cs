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
                SELECT p.Id AS PessoaId,e.Id AS EmpresaId,p.Nome,e.RazaoSocial,p.Email,p.Telefone,l.Cargo
                FROM dbo.PessoasEmpresas l WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Pessoas p ON p.Id=l.PessoaId JOIN dbo.Empresas e ON e.Id=l.EmpresaId
                WHERE l.PessoaId=@pessoa AND l.EmpresaId=@empresa AND l.StatusLead='QUALIFICADO' AND l.StatusVinculo='ATIVO'
                AND p.DataExclusao IS NULL AND p.DataBloqueio IS NULL AND p.Status='VALIDADO' AND e.Status='VALIDADA'
                """, r => new LeadExportado(r.Int("PessoaId"), r.Int("EmpresaId"), r.Text("Nome")!, r.Text("RazaoSocial")!, r.Text("Email"), r.Text("Telefone"), r.Text("Cargo")), Int("@pessoa", pessoa), Int("@empresa", empresa));
            if (rows.Count != 1) throw new ConflitoException("Exporte somente leads qualificados com vínculo ativo, pessoa validada e desbloqueada e empresa validada.");
            result.Add(rows[0]);
        }
        var id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.Exportacoes (QtdItens,UsuarioId) OUTPUT INSERTED.Id VALUES (@quantidade,@usuario)", r => r.GetInt32(0), Int("@quantidade", result.Count), Int("@usuario", usuarioId))).Single();
        foreach (var (pessoa, empresa) in pares)
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.ExportacoesPessoasEmpresas (ExportacoesId,PessoaId,EmpresaId) VALUES (@id,@pessoa,@empresa)", Int("@id", id), Int("@pessoa", pessoa), Int("@empresa", empresa));
        await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "EXPORTACAO", $"Exportação {id}: {result.Count} leads em CSV.");
        return result;
    });
}
