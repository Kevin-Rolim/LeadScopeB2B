using LeadScopeB2B.Data;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class CnaeRepository(SqlLeadScopeB2B sql)
{
    public Task<List<Cnaes>> ListarAsync(string? busca = null) => sql.ConsultarAsync("SELECT TOP (200) Numero,Descricao FROM dbo.vw_Cnaes WHERE @busca IS NULL OR CHARINDEX(@busca,Numero)>0 OR CHARINDEX(@busca,Descricao)>0 ORDER BY Numero", r => new Cnaes { Numero = r.Text("Numero")!, Descricao = r.Text("Descricao")! }, Text("@busca", busca, 200));
    public Task<List<CnaesEmpresas>> ListarDaEmpresaAsync(int empresaId) => sql.ConsultarAsync("SELECT Numero,Descricao FROM dbo.fn_CnaesDaEmpresa(@id) ORDER BY Numero", r => new CnaesEmpresas { EmpresaId = empresaId, CnaeNumero = r.Text("Numero")!, Cnaes = new Cnaes { Numero = r.Text("Numero")!, Descricao = r.Text("Descricao")! } }, Int("@id", empresaId));
    public async Task AssociarAsync(int empresaId, string numero)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.vw_Empresas WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND ISNULL(Status,'')<>'REMOVIDA'", r => r.GetInt32(0), Int("@id", empresaId));
            if (valid.Count == 0) throw new KeyNotFoundException("Empresa não encontrada.");
            await SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_CnaesEmpresasAssociar",
                Int("@EmpresaId", empresaId), Text("@CnaeNumero", numero, 20));
            return true;
        });
    }
    public Task RemoverAsync(int empresaId, string numero) => sql.ExecutarProcedureAsync("dbo.usp_CnaesEmpresasRemover",
        Int("@EmpresaId", empresaId), Text("@CnaeNumero", numero, 20));
}
