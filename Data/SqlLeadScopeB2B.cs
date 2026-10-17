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

    internal static async Task<List<T>> ConsultarAsync<T>(SqlConnection conn, SqlTransaction? transaction,
        string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, conn, transaction);
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
