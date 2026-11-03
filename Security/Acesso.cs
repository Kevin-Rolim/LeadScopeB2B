using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LeadScopeB2B.Repositories.Interfaces;

namespace LeadScopeB2B.Security;

public static class Acesso
{
    public const string Api = "Integracao";
    public const string Edicao = "Edicao";
    public const string Admin = "Admin";
    public static string? Papel(Usuarios usuario) => usuario.PerfisAcesso.ConjuntoPermissoes.Nome switch
    {
        "Admin" => "ADMIN",
        "Gestor" => "OPERACAO",
        "Leitura" => "LEITURA",
        _ => null
    };
    public static string Versao(Usuarios usuario) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{usuario.SenhaHash}:{usuario.Nome}:{usuario.Email}:{usuario.PerfilAcessoId}")));
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
