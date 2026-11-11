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
            var rows = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT PessoaId,EmpresaId,Nome,RazaoSocial,Email,Telefone,Cargo FROM dbo.fn_LeadExportavel(@pessoa,@empresa)",
                r => new LeadExportado(r.Int("PessoaId"), r.Int("EmpresaId"), r.Text("Nome")!, r.Text("RazaoSocial")!, r.Text("Email"), r.Text("Telefone"), r.Text("Cargo")), Int("@pessoa", pessoa), Int("@empresa", empresa));
            if (rows.Count != 1) throw new ConflitoException("Exporte somente leads qualificados com vínculo ativo, pessoa validada e desbloqueada e empresa validada.");
            result.Add(rows[0]);
        }
        var id = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_ExportacoesAdd", "@NovoId",
            Int("@QtdItens", result.Count), Int("@UsuarioId", usuarioId));
        foreach (var (pessoa, empresa) in pares)
            await SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_ExportacoesPessoasEmpresasAdd",
                Int("@ExportacoesId", id), Int("@PessoaId", pessoa), Int("@EmpresaId", empresa));
        await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "EXPORTACAO", $"Exportação {id}: {result.Count} leads em CSV.");
        return result;
    });
}
