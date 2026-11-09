using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Data.SqlClient;

namespace LeadScopeB2B.Services;

public record ResultadoCsv(long Registro, bool Sucesso, string Mensagem);

public class ImportacaoCsvService(EmpresaService empresas, PessoaService pessoas)
{
    public async Task<List<ResultadoCsv>> ImportarAsync(Stream stream, string tipo)
    {
        if (tipo is not ("empresas" or "pessoas")) throw new ValidationException("Tipo de cadastro inválido.");
        using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true, true);
        parser.HasFieldsEnclosedInQuotes = true;
        parser.SetDelimiters(";");
        var header = parser.ReadFields() ?? throw new ValidationException("CSV vazio.");
        var allowed = tipo == "empresas" ? new[] { "RazaoSocial", "Cnpj", "NomeFantasia", "Email", "Telefone", "Cidade", "Estado" } : new[] { "Nome", "Email", "Telefone", "UrlLinkedIn" };
        if (header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Length || header.Any(h => !allowed.Contains(h, StringComparer.OrdinalIgnoreCase)) || !header.Contains(allowed[0], StringComparer.OrdinalIgnoreCase))
            throw new ValidationException("Cabeçalho inválido. Colunas aceitas: " + string.Join(';', allowed));
        var resultados = new List<ResultadoCsv>();
        while (!parser.EndOfData)
        {
            var linha = parser.LineNumber;
            if (resultados.Count >= 1000) { resultados.Add(new(linha, false, "Limite de 1000 registros; restante não processado.")); break; }
            try
            {
                var fields = parser.ReadFields()!;
                if (fields.Length != header.Length) throw new ValidationException("Quantidade de campos diferente do cabeçalho.");
                var row = header.Zip(fields).ToDictionary(x => x.First, x => x.Second.Trim(), StringComparer.OrdinalIgnoreCase);
                string? Get(string key) => row.GetValueOrDefault(key) is string v && v.Length > 0 ? v : null;
                if (tipo == "empresas")
                {
                    if (Get("Cnpj") is null) throw new ValidationException("CNPJ obrigatório na importação para identificar duplicidades.");
                    await empresas.CadastrarAsync(new() { RazaoSocial = Get("RazaoSocial") ?? "", Cnpj = Get("Cnpj"), NomeFantasia = Get("NomeFantasia"), Email = Get("Email"), Telefone = Get("Telefone"), Cidade = Get("Cidade"), Estado = Get("Estado") });
                }
                else
                {
                    if (Get("Email") is null && Get("UrlLinkedIn") is null) throw new ValidationException("Informe Email ou UrlLinkedIn para identificar duplicidades.");
                    await pessoas.CadastrarAsync(new() { Nome = Get("Nome") ?? "", Email = Get("Email"), Telefone = Get("Telefone"), UrlLinkedIn = Get("UrlLinkedIn"), Origem = "Importação CSV" });
                }
                resultados.Add(new(linha, true, "Cadastrado."));
            }
            catch (Exception ex) when (ex is ValidationException or ConflitoException or MalformedLineException)
            { resultados.Add(new(linha, false, ex.Message)); }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            { resultados.Add(new(linha, false, "Registro duplicado.")); }
        }
        return resultados;
    }
}
