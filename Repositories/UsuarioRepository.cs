using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class UsuarioRepository(SqlLeadScopeB2B sql) : IUsuarioRepository
{
    private const string BuscarAdministrador =
        "SELECT TOP (1) UsuarioId FROM dbo.vw_UsuariosPerfis WHERE ConjuntoPermissoes='Administrador'";

    private const string Select = """
        SELECT UsuarioId AS Id, Usuario AS Nome, Email, PerfilId AS PerfilAcessoId,
               Perfil AS PerfilNome, ConjuntoId AS ConjuntoPermissoesId, ConjuntoPermissoes AS ConjuntoNome
        FROM dbo.vw_UsuariosPerfis
        """;

    private static Usuarios Mapear(SqlDataReader r, bool incluirSenha) => new()
    {
        Id = r.Int("Id"), Nome = r.Text("Nome")!, Email = r.Text("Email")!,
        SenhaHash = incluirSenha ? r.Text("SenhaHash")! : string.Empty, PerfilAcessoId = r.Int("PerfilAcessoId"),
        PerfisAcesso = new PerfisAcesso { Id = r.Int("PerfilAcessoId"), Nome = r.Text("PerfilNome")!, ConjuntoPermissoesId = r.Int("ConjuntoPermissoesId"), ConjuntoPermissoes = new ConjuntoPermissoes { Id = r.Int("ConjuntoPermissoesId"), Nome = r.Text("ConjuntoNome")! } }
    };

    private static Usuarios MapearCredenciais(SqlDataReader r) => Mapear(r, true);

    public Task<List<Usuarios>> ListarAsync() =>
        sql.ConsultarAsync(Select + " ORDER BY Usuario,UsuarioId", r => Mapear(r, false));

    public async Task<Usuarios?> ObterPorIdAsync(int id) =>
        (await sql.ConsultarProcedureAsync("dbo.usp_UsuariosObterPorId", MapearCredenciais, Int("@Id", id))).SingleOrDefault();

    public async Task<Usuarios?> ObterPorEmailAsync(string email) =>
        (await sql.ConsultarProcedureAsync("dbo.usp_UsuariosObterPorEmail", MapearCredenciais, Text("@Email", email, 150))).SingleOrDefault();

    public async Task<bool> EmailExisteAsync(string email, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT TOP (1) Id FROM dbo.vw_Usuarios WHERE LOWER(LTRIM(RTRIM(Email)))=@email AND (@id IS NULL OR Id<>@id)", r => r.GetInt32(0), Text("@email", email, 150), Int("@id", ignorarId))).Count > 0;
    public Task<List<PerfisAcesso>> ListarPerfisAsync() => sql.ConsultarAsync("SELECT Id,Nome,ConjuntoPermissoesId FROM dbo.vw_PerfisAcesso ORDER BY Nome", r => new PerfisAcesso { Id = r.Int("Id"), Nome = r.Text("Nome")!, ConjuntoPermissoesId = r.Int("ConjuntoPermissoesId") });
    public async Task<bool> PerfilExisteAsync(int perfilId) => (await sql.ConsultarAsync("SELECT Id FROM dbo.vw_PerfisAcesso WHERE Id=@id", r => r.GetInt32(0), Int("@id", perfilId))).Count > 0;

    private static SqlParameter[] Parametros(Usuarios u) =>
    [
        Text("@Nome", u.Nome, 100), Text("@Email", u.Email, 150),
        Text("@SenhaHash", u.SenhaHash, 255), Int("@PerfilAcessoId", u.PerfilAcessoId)
    ];

    public async Task AdicionarAsync(Usuarios usuario) =>
        usuario.Id = await sql.ExecutarProcedureComSaidaAsync("dbo.usp_UsuariosAdd", "@NovoId", Parametros(usuario));

    public async Task AtualizarAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT TOP (2) UsuarioId FROM dbo.vw_UsuariosPerfis WITH (UPDLOCK,HOLDLOCK) WHERE ConjuntoPermissoes='Administrador'",
                r => r.GetInt32(0));
            var perfil = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT ConjuntoPermissoes FROM dbo.vw_PerfisConjuntos WHERE Id=@id", r => r.GetString(0), Int("@id", usuario.PerfilAcessoId));
            if (admins.Count == 1 && admins[0] == usuario.Id && !perfil.Contains("Administrador"))
                throw new ConflitoException("O último administrador deve ser preservado.");
            try
            {
                await SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_UsuariosAlt",
                    [..Parametros(usuario), Int("@Id", usuario.Id)]);
            }
            catch (SqlException ex) when (ex.Number == 51001)
            {
                throw new KeyNotFoundException("Usuário não encontrado.", ex);
            }
            return true;
        });
    }
    public async Task CadastrarPublicoAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) Id FROM dbo.vw_Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(Email)))=@email", r => r.GetInt32(0), Text("@email", usuario.Email, 150));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível criar a conta com estes dados. Se já possui cadastro, entre ou recupere seu acesso.");
            var perfis = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) Id FROM dbo.vw_PerfisConjuntos WHERE ConjuntoPermissoes='Usuário' ORDER BY Id", r => r.GetInt32(0));
            if (perfis.Count == 0) throw new ConflitoException("O cadastro está indisponível. Entre em contato com o administrador.");
            usuario.PerfilAcessoId = perfis[0];
            usuario.Id = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_UsuariosAdd", "@NovoId", Parametros(usuario));
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuario.Id, "CADASTRO_PUBLICO", "Conta de usuário criada pelo cadastro público.");
            return true;
        });
    }
    public async Task<Usuarios> AtualizarPerfilAsync(int id, string nome, string email, string senhaEsperada)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) Id FROM dbo.vw_Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(Email)))=@email AND Id<>@id", r => r.GetInt32(0), Text("@email", email, 150), Int("@id", id));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível usar este e-mail. Informe outro endereço.");
            var count = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_UsuariosAlterarPerfil", "@Linhas",
                Int("@Id", id), Text("@Nome", nome, 100), Text("@Email", email, 150), Text("@SenhaEsperada", senhaEsperada, 255));
            if (count != 1) throw new ConflitoException("Sua conta foi alterada. Entre novamente para continuar.");
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, "ALTERACAO_PERFIL", "Dados pessoais atualizados pelo titular.");
            return (await SqlLeadScopeB2B.ConsultarProcedureAsync(conn, tx, "dbo.usp_UsuariosObterPorId", MapearCredenciais, Int("@Id", id))).Single();
        });
    }
    public async Task<bool> AtualizarSenhaAsync(int id, string email, string senhaEsperada, string novaSenha, string evento)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var count = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_UsuariosAlterarSenha", "@Linhas",
                Int("@Id", id), Text("@Email", email, 150), Text("@NovaSenha", novaSenha, 255), Text("@SenhaEsperada", senhaEsperada, 255));
            if (count != 1) return false;
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, evento, "Senha atualizada e sessões anteriores revogadas.");
            return true;
        });
    }
    public async Task<bool> AdministradorExisteAsync() =>
        (await sql.ConsultarAsync(BuscarAdministrador, r => r.GetInt32(0))).Count != 0;

    public async Task CriarAdministradorInicialAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            _ = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) Id FROM dbo.vw_Usuarios WITH (TABLOCKX,HOLDLOCK)", r => r.GetInt32(0));
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, BuscarAdministrador, r => r.GetInt32(0));
            if (admins.Count > 0) throw new ConflitoException("Já existe um administrador cadastrado. Use a administração de usuários.");
            var profiles = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) Id FROM dbo.vw_PerfisConjuntos WHERE ConjuntoPermissoes='Administrador' ORDER BY Id", r => r.GetInt32(0));
            usuario.PerfilAcessoId = profiles.FirstOrDefault();
            if (usuario.PerfilAcessoId == 0) throw new ConflitoException("O banco não possui perfil associado ao conjunto Administrador.");
            usuario.Id = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_UsuariosAdd", "@NovoId", Parametros(usuario));
            return true;
        });
    }
}
