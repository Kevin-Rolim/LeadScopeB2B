using System.ComponentModel.DataAnnotations;
using System.Text;
using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class ExportacaoService(ExportacaoRepository repository)
{
    public async Task<byte[]> ExportarAsync(string[] selecionados, int usuarioId)
    {
        if (selecionados.Length is 0 or > 200) throw new ValidationException("Selecione de 1 a 200 leads.");
        var pares = new List<(int Pessoa, int Empresa)>();
        foreach (var item in selecionados.Distinct())
        {
            var ids = item.Split(':');
            if (ids.Length != 2 || !int.TryParse(ids[0], out var p) || !int.TryParse(ids[1], out var e) || p <= 0 || e <= 0) throw new ValidationException("Seleção de leads inválida.");
            pares.Add((p, e));
        }
        var rows = await repository.ExportarAsync(pares, usuarioId);
        static string Cell(string? v)
        {
            v ??= "";
            if (v.TrimStart().StartsWith('=') || v.TrimStart().StartsWith('+') || v.TrimStart().StartsWith('-') || v.TrimStart().StartsWith('@') || v.StartsWith('\t') || v.StartsWith('\r')) v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("PessoaId;EmpresaId;Pessoa;Empresa;Email;Telefone;Cargo\r\n");
        foreach (var r in rows) csv.AppendLine(string.Join(';', new[] { r.PessoaId.ToString(), r.EmpresaId.ToString(), r.Pessoa, r.Empresa, r.Email, r.Telefone, r.Cargo }.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
