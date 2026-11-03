using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Security;

public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Sessao";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var usuarioId = Context.Session.GetInt32(SessaoUsuario.ChaveUsuarioId);

        if (!usuarioId.HasValue)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var nome = Context.Session.GetString(SessaoUsuario.ChaveNome) ?? "";
        var email = Context.Session.GetString(SessaoUsuario.ChaveEmail) ?? "";
        var papel = Context.Session.GetString(SessaoUsuario.ChavePapel) ?? "LEITURA";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()),
            new(ClaimTypes.Name, nome),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, papel)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/Conta/Login");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        Response.Redirect("/Conta/Negado");
        return Task.CompletedTask;
    }
}
