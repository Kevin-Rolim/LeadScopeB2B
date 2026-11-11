using System.Data;
using Microsoft.Data.SqlClient;

namespace LeadScopeB2B.Data;

// A conexão pertence à operação; nunca é compartilhada entre requisições.
public class SqlLeadScopeB2B(IConfiguration configuration)
{
    public SqlConnection Conn()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection para SQL Server.");
        return new SqlConnection(connectionString);
    }

    public async Task<List<T>> ConsultarAsync<T>(string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        return await ConsultarAsync(conn, null, sql, mapear, parametros);
    }

    internal static Task<List<T>> ConsultarAsync<T>(SqlConnection conn, SqlTransaction? transaction,
        string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros) =>
        ConsultarAsync(conn, transaction, sql, CommandType.Text, mapear, parametros);

    public async Task<List<T>> ConsultarProcedureAsync<T>(string nome, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        return await ConsultarProcedureAsync(conn, null, nome, mapear, parametros);
    }

    internal static Task<List<T>> ConsultarProcedureAsync<T>(SqlConnection conn, SqlTransaction? transaction,
        string nome, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros) =>
        ConsultarAsync(conn, transaction, nome, CommandType.StoredProcedure, mapear, parametros);

    private static async Task<List<T>> ConsultarAsync<T>(SqlConnection conn, SqlTransaction? transaction,
        string sql, CommandType tipo, Func<SqlDataReader, T> mapear, SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, conn, transaction) { CommandType = tipo };
        cmd.Parameters.AddRange(parametros);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<T>();
        while (await reader.ReadAsync()) result.Add(mapear(reader));
        return result;
    }

    public async Task<int> ExecutarAsync(string sql, params SqlParameter[] parametros)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        return await ExecutarAsync(conn, null, sql, parametros);
    }

    internal static async Task<int> ExecutarAsync(SqlConnection conn, SqlTransaction? transaction,
        string sql, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, conn, transaction);
        cmd.Parameters.AddRange(parametros);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task ExecutarProcedureAsync(string nome, params SqlParameter[] parametros)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await ExecutarProcedureAsync(conn, null, nome, parametros);
    }

    internal static async Task ExecutarProcedureAsync(SqlConnection conn, SqlTransaction? transaction,
        string nome, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(nome, conn, transaction) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddRange(parametros);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> ExecutarProcedureComSaidaAsync(string nome, string saida, params SqlParameter[] parametros)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        return await ExecutarProcedureComSaidaAsync(conn, null, nome, saida, parametros);
    }

    internal static async Task<int> ExecutarProcedureComSaidaAsync(SqlConnection conn, SqlTransaction? transaction,
        string nome, string saida, params SqlParameter[] parametros)
    {
        var parametro = new SqlParameter(saida, SqlDbType.Int) { Direction = ParameterDirection.Output };
        await ExecutarProcedureAsync(conn, transaction, nome, [..parametros, parametro]);
        return (int)parametro.Value;
    }

    public async Task<T> TransacaoAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> operacao)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await operacao(conn, transaction);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
