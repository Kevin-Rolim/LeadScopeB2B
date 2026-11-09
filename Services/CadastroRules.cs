using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace LeadScopeB2B.Services;

public static class CadastroRules
{
    public static string? Limpar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static void Validar(object model)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, true))
            throw new ValidationException(string.Join(" ", results.Select(r => r.ErrorMessage)));
    }
    public static string? Cnpj(string? value)
    {
        value = Limpar(value)?.ToUpperInvariant();
        if (value is null) return null;
        if (!Regex.IsMatch(value, @"^[A-Z0-9./\-\s]+$")) throw new ValidationException("CNPJ contém caracteres inválidos.");
        var digits = Regex.Replace(value, @"[./\-\s]", "");
        if (!Regex.IsMatch(digits, @"^[A-Z0-9]{12}[0-9]{2}$") || digits.Distinct().Count() == 1) throw new ValidationException("CNPJ inválido.");
        for (var length = 12; length <= 13; length++)
        {
            var sum = 0;
            for (var i = 0; i < length; i++) sum += (digits[i] - '0') * ((length - i - 1) % 8 + 2);
            var check = sum % 11 < 2 ? 0 : 11 - sum % 11;
            if (digits[length] - '0' != check) throw new ValidationException("CNPJ inválido.");
        }
        return digits;
    }
    public static string Estado(string? value, params string[] allowed)
    {
        var normalized = Limpar(value)?.ToUpperInvariant();
        if (normalized is null || !allowed.Contains(normalized)) throw new ValidationException("Estado de cadastro inválido: " + string.Join(", ", allowed) + ".");
        return normalized;
    }
    public static string? Url(string? value)
    {
        value = Limpar(value);
        if (value is null) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ValidationException("Informe uma URL HTTP ou HTTPS válida.");
        return value;
    }
}

public class ConflitoException(string message) : InvalidOperationException(message);
