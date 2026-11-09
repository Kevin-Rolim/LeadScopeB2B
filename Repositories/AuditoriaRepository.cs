using LeadScopeB2B.Data;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class AuditoriaRepository(SqlLeadScopeB2B sql)
{
    internal static Task<int> RegistrarAsync(SqlConnection conn, SqlTransaction tx, int usuarioId, string evento, string descricao) =>
        SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Logs (TipoEvento,DescricaoEvento,UsuarioId) VALUES (@evento,@descricao,@usuario)", Text("@evento", evento, 50), Text("@descricao", descricao), Int("@usuario", usuarioId));
    public Task RegistrarAsync(int usuarioId, string evento, string descricao) => sql.ExecutarAsync("INSERT INTO dbo.Logs (TipoEvento,DescricaoEvento,UsuarioId) VALUES (@evento,@descricao,@usuario)", Text("@evento", evento, 50), Text("@descricao", descricao), Int("@usuario", usuarioId));
    public Task<List<Logs>> ListarAsync() => sql.ConsultarAsync("SELECT TOP (200) l.*,u.Nome AS UsuarioNome FROM dbo.Logs l JOIN dbo.Usuarios u ON u.Id=l.UsuarioId ORDER BY l.DataHora DESC,l.Id DESC", r => new Logs { Id = r.Int("Id"), TipoEvento = r.Text("TipoEvento")!, DescricaoEvento = r.Text("DescricaoEvento"), DataHora = r.Date("DataHora"), UsuarioId = r.Int("UsuarioId"), Usuarios = new Usuarios { Nome = r.Text("UsuarioNome")! } });
}
