using LeadScopeB2B.Data;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class AuditoriaRepository(SqlLeadScopeB2B sql)
{
    internal static Task<int> RegistrarAsync(SqlConnection conn, SqlTransaction tx, int usuarioId, string evento, string descricao) =>
        SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Logs (tipo_evento,desc_evento,usuario_id) VALUES (@evento,@descricao,@usuario)", Text("@evento", evento, 80), Text("@descricao", descricao), Int("@usuario", usuarioId));
    public Task RegistrarAsync(int usuarioId, string evento, string descricao) => sql.ExecutarAsync("INSERT INTO dbo.Logs (tipo_evento,desc_evento,usuario_id) VALUES (@evento,@descricao,@usuario)", Text("@evento", evento, 80), Text("@descricao", descricao), Int("@usuario", usuarioId));
    public Task<List<Logs>> ListarAsync() => sql.ConsultarAsync("SELECT TOP (200) l.*,u.nome AS usuario_nome FROM dbo.Logs l JOIN dbo.Usuarios u ON u.id=l.usuario_id ORDER BY l.data_hora DESC,l.id DESC", r => new Logs { Id = r.Int("id"), TiporEvento = r.Text("tipo_evento")!, DescricaoEvento = r.Text("desc_evento"), DataHora = r.Date("data_hora")!.Value, UsuarioId = r.Int("usuario_id"), Usuarios = new Usuarios { Nome = r.Text("usuario_nome")! } });
}
