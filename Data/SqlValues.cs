using System.Data;
using Microsoft.Data.SqlClient;
namespace LeadScopeB2B.Data;

internal static class SqlValues
{
    public static SqlParameter Text(string name, string? value, int size = -1) => new(name, SqlDbType.NVarChar, size) { Value = (object?)value ?? DBNull.Value };
    public static SqlParameter Int(string name, int? value) => new(name, SqlDbType.Int) { Value = (object?)value ?? DBNull.Value };
    public static SqlParameter Long(string name, long value) => new(name, SqlDbType.BigInt) { Value = value };
    public static SqlParameter Bit(string name, bool value) => new(name, SqlDbType.Bit) { Value = value };
    public static SqlParameter Date(string name, DateTime? value) => new(name, SqlDbType.DateTime2) { Value = (object?)value ?? DBNull.Value };
    public static SqlParameter Decimal(string name, decimal? value) => new(name, SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = (object?)value ?? DBNull.Value };
    public static string? Text(this SqlDataReader r, string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : r.GetString(r.GetOrdinal(name));
    public static DateTime? Date(this SqlDataReader r, string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : r.GetDateTime(r.GetOrdinal(name));
    public static int Int(this SqlDataReader r, string name) => r.GetInt32(r.GetOrdinal(name));
    public static decimal? Decimal(this SqlDataReader r, string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : r.GetDecimal(r.GetOrdinal(name));
    public static bool Bit(this SqlDataReader r, string name) => r.GetBoolean(r.GetOrdinal(name));
}
