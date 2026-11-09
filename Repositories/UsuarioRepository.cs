using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public class UsuarioRepository(SqlLeadScopeB2B sql) : IUsuarioRepository
{
    private const string Select = """
        SELECT u.Id,u.Nome,u.Email,u.SenhaHash,u.PerfilAcessoId,p.Nome AS PerfilNome,p.ConjuntoPermissoesId,c.Nome AS ConjuntoNome
        FROM dbo.Usuarios u JOIN dbo.PerfisAcesso p ON p.Id=u.PerfilAcessoId JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId
        """;
    private static Usuarios Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("Id"), Nome = r.Text("Nome")!, Email = r.Text("Email")!, SenhaHash = r.Text("SenhaHash")!, PerfilAcessoId = r.Int("PerfilAcessoId"),
        PerfisAcesso = new PerfisAcesso { Id = r.Int("PerfilAcessoId"), Nome = r.Text("PerfilNome")!, ConjuntoPermissoesId = r.Int("ConjuntoPermissoesId"), ConjuntoPermissoes = new ConjuntoPermissoes { Id = r.Int("ConjuntoPermissoesId"), Nome = r.Text("ConjuntoNome")! } }
    };
    public Task<List<Usuarios>> ListarAsync() => sql.ConsultarAsync(Select + " ORDER BY u.Nome,u.Id", Mapear);
    public async Task<Usuarios?> ObterPorIdAsync(int id) => (await sql.ConsultarAsync(Select + " WHERE u.Id=@id", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<Usuarios?> ObterPorEmailAsync(string email) => (await sql.ConsultarAsync(Select + " WHERE LOWER(LTRIM(RTRIM(u.Email)))=@email", Mapear, Text("@email", email, 150))).SingleOrDefault();
    public async Task<bool> EmailExisteAsync(string email, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT Id FROM dbo.Usuarios WHERE LOWER(LTRIM(RTRIM(Email)))=@email AND (@id IS NULL OR Id<>@id)", r => r.GetInt32(0), Text("@email", email, 150), Int("@id", ignorarId))).Count > 0;
    public Task<List<PerfisAcesso>> ListarPerfisAsync() => sql.ConsultarAsync("SELECT Id,Nome,ConjuntoPermissoesId FROM dbo.PerfisAcesso ORDER BY Nome", r => new PerfisAcesso { Id = r.Int("Id"), Nome = r.Text("Nome")!, ConjuntoPermissoesId = r.Int("ConjuntoPermissoesId") });
    public async Task<bool> PerfilExisteAsync(int perfilId) => (await sql.ConsultarAsync("SELECT Id FROM dbo.PerfisAcesso WHERE Id=@id", r => r.GetInt32(0), Int("@id", perfilId))).Count > 0;
    private static SqlParameter[] Parametros(Usuarios u) => [Int("@id", u.Id), Text("@nome", u.Nome, 100), Text("@email", u.Email, 150), Text("@senha", u.SenhaHash, 255), Int("@perfil", u.PerfilAcessoId)];
    private const string Insert = "INSERT INTO dbo.Usuarios (Nome,Email,SenhaHash,PerfilAcessoId) OUTPUT INSERTED.Id VALUES (@nome,@email,@senha,@perfil)";
    public async Task AdicionarAsync(Usuarios usuario) => usuario.Id = (await sql.ConsultarAsync(Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
    public async Task AtualizarAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select.Replace("dbo.Usuarios u", "dbo.Usuarios u WITH (UPDLOCK,HOLDLOCK)") + " WHERE c.Nome='Administrador'", Mapear);
            var perfil = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT c.Nome FROM dbo.PerfisAcesso p JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId WHERE p.Id=@id", r => r.GetString(0), Int("@id", usuario.PerfilAcessoId));
            if (admins.Count == 1 && admins[0].Id == usuario.Id && !perfil.Contains("Administrador")) throw new ConflitoException("O último administrador deve ser preservado.");
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Usuarios SET Nome=@nome,Email=@email,SenhaHash=@senha,PerfilAcessoId=@perfil WHERE Id=@id", Parametros(usuario)) == 0) throw new KeyNotFoundException("Usuário não encontrado.");
            return true;
        });
    }
    public async Task CadastrarPublicoAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(Email)))=@email", r => r.GetInt32(0), Text("@email", usuario.Email, 150));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível criar a conta com estes dados. Se já possui cadastro, entre ou recupere seu acesso.");
            var perfis = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT TOP (1) p.Id FROM dbo.PerfisAcesso p JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId WHERE c.Nome='Usuário' ORDER BY p.Id", r => r.GetInt32(0));
            if (perfis.Count == 0) throw new ConflitoException("O cadastro está indisponível. Entre em contato com o administrador.");
            usuario.PerfilAcessoId = perfis[0];
            usuario.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuario.Id, "CADASTRO_PUBLICO", "Conta de usuário criada pelo cadastro público.");
            return true;
        });
    }
    public async Task<Usuarios> AtualizarPerfilAsync(int id, string nome, string email, string senhaEsperada)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(Email)))=@email AND Id<>@id", r => r.GetInt32(0), Text("@email", email, 150), Int("@id", id));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível usar este e-mail. Informe outro endereço.");
            var count = await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Usuarios SET Nome=@nome,Email=@email WHERE Id=@id AND SenhaHash COLLATE Latin1_General_100_BIN2=@senha", Int("@id", id), Text("@nome", nome, 100), Text("@email", email, 150), Text("@senha", senhaEsperada, 255));
            if (count != 1) throw new ConflitoException("Sua conta foi alterada. Entre novamente para continuar.");
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, "ALTERACAO_PERFIL", "Dados pessoais atualizados pelo titular.");
            return (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select + " WHERE u.Id=@id", Mapear, Int("@id", id))).Single();
        });
    }
    public async Task<bool> AtualizarSenhaAsync(int id, string email, string senhaEsperada, string novaSenha, string evento)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var count = await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Usuarios SET SenhaHash=@nova WHERE Id=@id AND Email=@email AND SenhaHash COLLATE Latin1_General_100_BIN2=@anterior", Int("@id", id), Text("@email", email, 150), Text("@nova", novaSenha, 255), Text("@anterior", senhaEsperada, 255));
            if (count != 1) return false;
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, evento, "Senha atualizada e sessões anteriores revogadas.");
            return true;
        });
    }
    public async Task CriarAdministradorInicialAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            _ = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.Usuarios WITH (TABLOCKX,HOLDLOCK)", r => r.GetInt32(0));
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select + " WHERE c.Nome='Administrador'", Mapear);
            if (admins.Count > 0) throw new ConflitoException("Já existe um administrador cadastrado. Use a administração de usuários.");
            var profiles = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT p.Id FROM dbo.PerfisAcesso p JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId WHERE c.Nome='Administrador' ORDER BY p.Id", r => r.GetInt32(0));
            usuario.PerfilAcessoId = profiles.FirstOrDefault();
            if (usuario.PerfilAcessoId == 0) throw new ConflitoException("O banco não possui perfil associado ao conjunto Administrador.");
            usuario.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
            return true;
        });
    }
}
