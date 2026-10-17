using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LeadScopeB2B.Security;

public static class Acesso
{
    public const string Api = "Integracao";
    public const string Edicao = "Edicao";
    public const string Admin = "Admin";
    public static string? Papel(Usuarios usuario) => usuario.PerfisAcesso.ConjuntoPermissoes.Nome switch
    {
        "Administracao" or "Admin" => "ADMIN",
        "Operacao Comercial" or "Gestor" => "OPERACAO",
        "Leitura" => "LEITURA",
        _ => null
    };
    public static string Versao(Usuarios usuario) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{usuario.Senha}:{usuario.VersaoSessao}")));
    public static ClaimsPrincipal Principal(Usuarios usuario, string scheme)
    {
        var role = Papel(usuario) ?? throw new InvalidOperationException("Conjunto de permissões não reconhecido.");
        return new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()), new(ClaimTypes.Name,usuario.Nome),
            new(ClaimTypes.Email,usuario.Email), new(ClaimTypes.Role,role), new("versao",Versao(usuario))
        ], scheme));
    }
    public static int UsuarioId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// Revalida bloqueio, exclusão, troca de senha e perfil a cada requisição.
public class SessaoEvents(IUsuarioRepository usuarios) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { context.RejectPrincipal(); return; }
        var usuario = await usuarios.ObterPorIdAsync(id);
        if (usuario is null || usuario.Status != "ATIVO" || Acesso.Papel(usuario) is null || context.Principal?.FindFirstValue("versao") != Acesso.Versao(usuario))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }
        context.ReplacePrincipal(Acesso.Principal(usuario, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
