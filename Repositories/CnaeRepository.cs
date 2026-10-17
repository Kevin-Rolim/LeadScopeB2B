using LeadScopeB2B.Data;
using LeadScopeB2B.Services;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class CnaeRepository(SqlLeadScopeB2B sql)
{
    public Task<List<Cnaes>> ListarAsync(string? busca = null) => sql.ConsultarAsync("SELECT TOP (200) numero,descricao FROM dbo.CNAEs WHERE @busca IS NULL OR CHARINDEX(@busca,numero)>0 OR CHARINDEX(@busca,descricao)>0 ORDER BY numero", r => new Cnaes { Numero = r.Text("numero")!, Descricao = r.Text("descricao")! }, Text("@busca", busca, 200));
    public Task AdicionarAsync(Cnaes model) => sql.ExecutarAsync("INSERT INTO dbo.CNAEs (numero,descricao) VALUES (@numero,@descricao)", Text("@numero", model.Numero, 7), Text("@descricao", model.Descricao, 300));
    public Task<List<CnaesEmpresas>> ListarDaEmpresaAsync(int empresaId) => sql.ConsultarAsync("SELECT c.numero,c.descricao,ce.principal FROM dbo.CNAEs_Empresas ce JOIN dbo.CNAEs c ON c.numero=ce.cnae_numero WHERE ce.empresa_id=@id ORDER BY ce.principal DESC,c.numero", r => new CnaesEmpresas { EmpresaId = empresaId, CnaeNumero = r.Text("numero")!, Principal = r.Bit("principal"), Cnaes = new Cnaes { Numero = r.Text("numero")!, Descricao = r.Text("descricao")! } }, Int("@id", empresaId));
    public async Task AssociarAsync(int empresaId, string numero, bool principal)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT id FROM dbo.Empresas WITH (UPDLOCK,HOLDLOCK) WHERE id=@id AND status_validacao<>'REMOVIDA'", r => r.GetInt32(0), Int("@id", empresaId));
            if (valid.Count == 0) throw new KeyNotFoundException("Empresa não encontrada.");
            if (principal) await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.CNAEs_Empresas SET principal=0 WHERE empresa_id=@id", Int("@id", empresaId));
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "IF EXISTS(SELECT 1 FROM dbo.CNAEs_Empresas WHERE empresa_id=@id AND cnae_numero=@numero) UPDATE dbo.CNAEs_Empresas SET principal=@principal WHERE empresa_id=@id AND cnae_numero=@numero ELSE INSERT INTO dbo.CNAEs_Empresas (empresa_id,cnae_numero,principal) VALUES (@id,@numero,@principal)", Int("@id", empresaId), Text("@numero", numero, 7), Bit("@principal", principal));
            return true;
        });
    }
    public Task RemoverAsync(int empresaId, string numero) => sql.ExecutarAsync("DELETE FROM dbo.CNAEs_Empresas WHERE empresa_id=@id AND cnae_numero=@numero", Int("@id", empresaId), Text("@numero", numero, 7));
}
