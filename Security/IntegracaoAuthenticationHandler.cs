using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Security;

public class ChaveIntegracao
{
    public string Sha256 { get; set; } = "";
    public int UsuarioId { get; set; }
    public int FonteDadosId { get; set; }
    public bool PermiteEnvio { get; set; }
}

public class IntegracaoAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration configuration, IUsuarioRepository usuarios, IWebHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var header)) return AuthenticateResult.NoResult();
        if (!Request.IsHttps && !environment.IsDevelopment()) return AuthenticateResult.Fail("HTTPS obrigatório.");
        if (header.Count != 1 || string.IsNullOrEmpty(header[0]) || header[0]!.Length > 512) return AuthenticateResult.Fail("Chave inválida.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(header[0]!));
        ChaveIntegracao? match = null;
        foreach (var key in configuration.GetSection("Integracoes:Chaves").Get<List<ChaveIntegracao>>() ?? [])
        {
            if (key.Sha256.Length != 64) continue;
            byte[] expected;
            try { expected = Convert.FromHexString(key.Sha256); } catch (FormatException) { continue; }
            if (CryptographicOperations.FixedTimeEquals(hash, expected)) { match = key; break; }
        }
        if (match is null) return AuthenticateResult.Fail("Chave inválida.");
        var usuario = await usuarios.ObterPorIdAsync(match.UsuarioId);
        if (usuario is null || usuario.Status != "ATIVO" || Acesso.Papel(usuario) is null) return AuthenticateResult.Fail("Usuário indisponível.");
        var principal = Acesso.Principal(usuario, Acesso.Api);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new("fonte", match.FonteDadosId.ToString()));
        if (match.PermiteEnvio && Acesso.Papel(usuario) is "ADMIN" or "OPERACAO") identity.AddClaim(new("envio", "true"));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Acesso.Api));
    }
}
