using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LeadScopeB2B.Security;

public record RecuperacaoPayload(int UsuarioId, string Email, string Versao);

public class RecuperacaoToken(IDataProtectionProvider provider)
{
    public const string Proposito = "LeadScopeB2B.RecuperacaoAcesso.v1";
    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector(Proposito).ToTimeLimitedDataProtector();

    public string Criar(Usuarios usuario) => _protector.Protect(
        JsonSerializer.Serialize(new RecuperacaoPayload(usuario.Id, usuario.Email, Acesso.Versao(usuario))), TimeSpan.FromHours(1));

    public RecuperacaoPayload? Ler(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        try { return JsonSerializer.Deserialize<RecuperacaoPayload>(_protector.Unprotect(token)); }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException) { return null; }
    }
}
