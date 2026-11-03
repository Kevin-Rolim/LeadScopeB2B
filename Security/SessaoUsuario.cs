namespace LeadScopeB2B.Security;

public static class SessaoUsuario
{
    public const string ChaveUsuarioId = "UsuarioId";
    public const string ChaveNome = "Nome";
    public const string ChaveEmail = "Email";
    public const string ChavePapel = "Papel";

    public static void Gravar(ISession session, Usuarios usuario)
    {
        session.SetInt32(ChaveUsuarioId, usuario.Id);
        session.SetString(ChaveNome, usuario.Nome);
        session.SetString(ChaveEmail, usuario.Email);
        session.SetString(ChavePapel, Acesso.Papel(usuario) ?? "LEITURA");
    }

    public static void Limpar(ISession session)
    {
        session.Clear();
    }

    public static int? ObterUsuarioId(ISession session) => session.GetInt32(ChaveUsuarioId);
    public static string? ObterNome(ISession session) => session.GetString(ChaveNome);
    public static string? ObterEmail(ISession session) => session.GetString(ChaveEmail);
    public static string? ObterPapel(ISession session) => session.GetString(ChavePapel);
    public static bool EstaAutenticado(ISession session) => session.GetInt32(ChaveUsuarioId).HasValue;
}
