using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using LeadScopeB2B.Models;

internal static class SchemaContractTests
{
    public static void Executar(string root)
    {
        var schema = File.ReadAllText(Path.Combine(root, "Docs/Database/LeadScopeB2B.sql"));
        var tables = Regex.Matches(schema, @"CREATE TABLE (\w+) \((.*?)^    \)", RegexOptions.Singleline | RegexOptions.Multiline);
        if (tables.Count != 14) throw new Exception("O contrato deve conter as 14 tabelas do banco definitivo.");
        var context = new NullabilityInfoContext();
        foreach (Match table in tables)
        {
            var name = table.Groups[1].Value;
            var type = typeof(Usuarios).Assembly.GetType("LeadScopeB2B.Models." + name) ?? throw new Exception("Model ausente: " + name);
            var properties = type.GetProperties().Where(p => p.PropertyType == typeof(string) || (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsValueType).ToDictionary(p => p.Name, StringComparer.Ordinal);
            var columns = Regex.Matches(table.Groups[2].Value, @"^\s+(\w+) (INT|VARCHAR|CHAR|DATETIME2|DECIMAL)\b([^\r\n]*)", RegexOptions.Multiline);
            foreach (Match column in columns)
            {
                var columnName = column.Groups[1].Value;
                if (!properties.Remove(columnName, out var property)) throw new Exception($"{name}.{columnName}: nome exato ausente no model.");
                var expectedType = column.Groups[2].Value switch { "INT" => typeof(int), "DATETIME2" => typeof(DateTime), "DECIMAL" => typeof(decimal), _ => typeof(string) };
                if ((Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) != expectedType) throw new Exception($"{name}.{columnName}: tipo incompatível.");
                var required = Regex.IsMatch(column.Groups[3].Value, @"NOT NULL|PRIMARY KEY");
                var nullable = property.PropertyType == typeof(string) ? context.Create(property).ReadState == NullabilityState.Nullable : Nullable.GetUnderlyingType(property.PropertyType) is not null;
                if (required == nullable) throw new Exception($"{name}.{columnName}: nulabilidade incompatível.");
                if (expectedType == typeof(string) && Regex.Match(column.Groups[3].Value, @"^\((\d+)\)") is { Success: true } length)
                {
                    var limit = property.GetCustomAttribute<StringLengthAttribute>();
                    if (limit is null || limit.MaximumLength > int.Parse(length.Groups[1].Value)) throw new Exception($"{name}.{columnName}: limite de tamanho incompatível.");
                }
            }
            if (properties.Count > 0) throw new Exception($"{name}: propriedades sem coluna: {string.Join(", ", properties.Keys)}.");
            Console.WriteLine($"OK: {name} — nomes, tipos, nulabilidade e tamanhos ({columns.Count} colunas)");
        }
    }
}
