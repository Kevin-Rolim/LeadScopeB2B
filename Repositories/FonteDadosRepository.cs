using LeadScopeB2B.Data;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class FonteDadosRepository(SqlLeadScopeB2B sql)
{
    public Task<List<FonteDados>> ListarAsync() => sql.ConsultarAsync("SELECT id,nome,tipo,url,confiabilidade,ativo,data_hora_mod,data_hora_del FROM dbo.Fontes_dados ORDER BY nome", r => new FonteDados { Id = r.Int("id"), Nome = r.Text("nome")!, Tipo = r.Text("tipo"), Url = r.Text("url"), Confiabilidade = r.Decimal("confiabilidade"), Ativo = r.Bit("ativo"), DataHoraMod = r.Date("data_hora_mod")!.Value, DataHoraDel = r.Date("data_hora_del") });
    public async Task<bool> EstaAtivaAsync(int id) => (await sql.ConsultarAsync("SELECT id FROM dbo.Fontes_dados WHERE id=@id AND ativo=1 AND data_hora_del IS NULL", r => r.GetInt32(0), Int("@id", id))).Count > 0;
    public Task AdicionarAsync(FonteDados model) => sql.ExecutarAsync("INSERT INTO dbo.Fontes_dados (nome,tipo,url,confiabilidade) VALUES (@nome,@tipo,@url,@confianca)", Text("@nome", model.Nome, 100), Text("@tipo", model.Tipo, 30), Text("@url", model.Url, 255), Decimal("@confianca", model.Confiabilidade));
    public async Task AlterarAtivoAsync(int id, bool ativo)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.Fontes_dados SET ativo=@ativo,data_hora_mod=SYSUTCDATETIME(),data_hora_del=CASE WHEN @ativo=1 THEN NULL ELSE SYSUTCDATETIME() END WHERE id=@id", Int("@id", id), Bit("@ativo", ativo)) == 0) throw new KeyNotFoundException("Fonte não encontrada.");
    }
}
