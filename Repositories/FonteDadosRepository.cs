using LeadScopeB2B.Data;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class FonteDadosRepository(SqlLeadScopeB2B sql)
{
    public Task<List<FonteDados>> ListarAsync() => sql.ConsultarAsync("SELECT Id,Nome,Tipo,Url,Confiabilidade,DataHoraModificacao,DataHoraExclusao FROM dbo.FonteDados ORDER BY Nome", r => new FonteDados { Id = r.Int("Id"), Nome = r.Text("Nome")!, Tipo = r.Text("Tipo"), Url = r.Text("Url"), Confiabilidade = r.Decimal("Confiabilidade"), DataHoraModificacao = r.Date("DataHoraModificacao"), DataHoraExclusao = r.Date("DataHoraExclusao") });
    public async Task<bool> EstaAtivaAsync(int id) => (await sql.ConsultarAsync("SELECT Id FROM dbo.FonteDados WHERE Id=@id AND DataHoraExclusao IS NULL", r => r.GetInt32(0), Int("@id", id))).Count > 0;
    public Task AdicionarAsync(FonteDados model) => sql.ExecutarAsync("INSERT INTO dbo.FonteDados (Nome,Tipo,Url,Confiabilidade) VALUES (@nome,@tipo,@url,@confianca)", Text("@nome", model.Nome, 100), Text("@tipo", model.Tipo, 50), Text("@url", model.Url, 255), Decimal("@confianca", model.Confiabilidade));
    public async Task AlterarAtivoAsync(int id, bool ativo)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.FonteDados SET DataHoraModificacao=SYSUTCDATETIME(),DataHoraExclusao=CASE WHEN @ativo=1 THEN NULL ELSE SYSUTCDATETIME() END WHERE Id=@id", Int("@id", id), Bit("@ativo", ativo)) == 0) throw new KeyNotFoundException("Fonte não encontrada.");
    }
}
