using LeadScopeB2B.Data;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class AuditoriaRepository(SqlLeadScopeB2B sql)
{
    internal static Task RegistrarAsync(SqlConnection conn, SqlTransaction tx, int usuarioId, string evento, string descricao) =>
        SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_LogsAdd", Text("@TipoEvento", evento, 50), Text("@DescricaoEvento", descricao), Int("@UsuarioId", usuarioId));
    public Task RegistrarAsync(int usuarioId, string evento, string descricao) => sql.ExecutarProcedureAsync("dbo.usp_LogsAdd", Text("@TipoEvento", evento, 50), Text("@DescricaoEvento", descricao), Int("@UsuarioId", usuarioId));
    public Task<List<Logs>> ListarAsync() => sql.ConsultarAsync("SELECT TOP (200) Id,TipoEvento,DescricaoEvento,DataHora,UsuarioId,UsuarioNome FROM dbo.vw_LogsUsuarios ORDER BY DataHora DESC,Id DESC", r => new Logs { Id = r.Int("Id"), TipoEvento = r.Text("TipoEvento")!, DescricaoEvento = r.Text("DescricaoEvento"), DataHora = r.Date("DataHora"), UsuarioId = r.Int("UsuarioId"), Usuarios = new Usuarios { Nome = r.Text("UsuarioNome")! } });
}
