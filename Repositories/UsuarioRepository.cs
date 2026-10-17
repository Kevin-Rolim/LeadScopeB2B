using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class UsuarioRepository(SqlLeadScopeB2B sql) : IUsuarioRepository
{
    private const string Select = """
        SELECT u.id,u.nome,u.email,u.senha_hash,u.versao_sessao,u.status,u.perfil_acesso_id,p.nome AS perfil_nome,p.conjunto_permissoes_id,c.nome AS conjunto_nome
        FROM dbo.Usuarios u JOIN dbo.Perfis_acesso p ON p.id=u.perfil_acesso_id JOIN dbo.Conjuntos_permissoes c ON c.id=p.conjunto_permissoes_id
        """;
    private static Usuarios Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("id"),
        Nome = r.Text("nome")!,
        Email = r.Text("email")!,
        Senha = r.Text("senha_hash")!,
        Status = r.Text("status")!,
        VersaoSessao = r.GetInt64(r.GetOrdinal("versao_sessao")),
        PerfilAcessoId = r.Int("perfil_acesso_id"),
        PerfisAcesso = new PerfisAcesso { Id = r.Int("perfil_acesso_id"), Nome = r.Text("perfil_nome")!, ConjuntoPermissoesId = r.Int("conjunto_permissoes_id"), ConjuntoPermissoes = new ConjuntoPermissoes { Id = r.Int("conjunto_permissoes_id"), Nome = r.Text("conjunto_nome")! } }
    };
    public Task<List<Usuarios>> ListarAsync() => sql.ConsultarAsync(Select + " ORDER BY u.nome,u.id", Mapear);
    public async Task<Usuarios?> ObterPorIdAsync(int id) => (await sql.ConsultarAsync(Select + " WHERE u.id=@id", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<Usuarios?> ObterPorEmailAsync(string email) => (await sql.ConsultarAsync(Select + " WHERE LOWER(LTRIM(RTRIM(u.email)))=@email", Mapear, Text("@email", email, 150))).SingleOrDefault();
    public async Task<bool> EmailExisteAsync(string email, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT id FROM dbo.Usuarios WHERE LOWER(LTRIM(RTRIM(email)))=@email AND (@id IS NULL OR id<>@id)", r => r.GetInt32(0), Text("@email", email, 150), Int("@id", ignorarId))).Count > 0;
    public Task<List<PerfisAcesso>> ListarPerfisAsync() => sql.ConsultarAsync("SELECT id,nome,conjunto_permissoes_id FROM dbo.Perfis_acesso ORDER BY nome", r => new PerfisAcesso { Id = r.Int("id"), Nome = r.Text("nome")!, ConjuntoPermissoesId = r.Int("conjunto_permissoes_id") });
    public async Task<bool> PerfilExisteAsync(int perfilId) => (await sql.ConsultarAsync("SELECT id FROM dbo.Perfis_acesso WHERE id=@id", r => r.GetInt32(0), Int("@id", perfilId))).Count > 0;
    private static SqlParameter[] Parametros(Usuarios u) => [Int("@id", u.Id), Text("@nome", u.Nome, 100), Text("@email", u.Email, 150), Text("@senha", u.Senha, 255), Int("@perfil", u.PerfilAcessoId), Text("@status", u.Status, 20), Long("@versao", u.VersaoSessao)];
    private const string Insert = "INSERT INTO dbo.Usuarios (nome,email,senha_hash,perfil_acesso_id,status) OUTPUT INSERTED.id VALUES (@nome,@email,@senha,@perfil,@status)";
    public async Task AdicionarAsync(Usuarios usuario) => usuario.Id = (await sql.ConsultarAsync(Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
    public async Task AtualizarAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            // Bloqueia os usuários durante a contagem: duas desativações não podem eliminar todos os administradores.
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select.Replace("dbo.Usuarios u", "dbo.Usuarios u WITH (UPDLOCK,HOLDLOCK)") + " WHERE u.status='ATIVO' AND c.nome IN ('Administracao','Admin')", Mapear);
            var perfil = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT c.nome FROM dbo.Perfis_acesso p JOIN dbo.Conjuntos_permissoes c ON c.id=p.conjunto_permissoes_id WHERE p.id=@id", r => r.GetString(0), Int("@id", usuario.PerfilAcessoId));
            if (admins.Count == 1 && admins[0].Id == usuario.Id && (usuario.Status != "ATIVO" || !perfil.Any(p => p is "Administracao" or "Admin")))
                throw new ConflitoException("O último administrador ativo deve ser preservado.");
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Usuarios SET nome=@nome,email=@email,senha_hash=@senha,perfil_acesso_id=@perfil,status=@status,versao_sessao=versao_sessao+1,data_atualizacao=SYSUTCDATETIME() WHERE id=@id AND versao_sessao=@versao", Parametros(usuario)) == 0)
                throw new ConflitoException("O usuário foi alterado por outra operação. Recarregue o cadastro.");
            return true;
        });
    }
    public async Task CadastrarPublicoAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT id FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(email)))=@email",
                r => r.GetInt32(0), Text("@email", usuario.Email, 150));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível criar a conta com estes dados. Se já possui cadastro, entre ou recupere seu acesso.");
            var perfis = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT TOP (1) p.id FROM dbo.Perfis_acesso p JOIN dbo.Conjuntos_permissoes c ON c.id=p.conjunto_permissoes_id WHERE c.nome='Leitura' ORDER BY p.id",
                r => r.GetInt32(0));
            if (perfis.Count == 0) throw new ConflitoException("O cadastro está indisponível. Entre em contato com o administrador.");
            usuario.PerfilAcessoId = perfis[0];
            usuario.Status = "ATIVO";
            usuario.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuario.Id, "CADASTRO_PUBLICO", "Conta de leitura criada pelo cadastro público.");
            return true;
        });
    }

    public async Task<Usuarios> AtualizarPerfilAsync(int id, string nome, string email, string senhaEsperada, long versaoEsperada)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT id FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(email)))=@email AND id<>@id",
                r => r.GetInt32(0), Text("@email", email, 150), Int("@id", id));
            if (duplicados.Count > 0) throw new ConflitoException("Não foi possível usar este e-mail. Informe outro endereço.");
            var count = await SqlLeadScopeB2B.ExecutarAsync(conn, tx, """
                UPDATE dbo.Usuarios SET nome=@nome,email=@email,versao_sessao=versao_sessao+1,data_atualizacao=SYSUTCDATETIME()
                WHERE id=@id AND status='ATIVO' AND senha_hash COLLATE Latin1_General_100_BIN2=@senha AND versao_sessao=@versao
                """, Int("@id", id), Text("@nome", nome, 100), Text("@email", email, 150), Text("@senha", senhaEsperada, 255), Long("@versao", versaoEsperada));
            if (count != 1) throw new ConflitoException("Sua conta foi alterada. Entre novamente para continuar.");
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, "ALTERACAO_PERFIL", "Dados pessoais atualizados pelo titular.");
            return (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select + " WHERE u.id=@id", Mapear, Int("@id", id))).Single();
        });
    }

    public async Task<bool> AtualizarSenhaAsync(int id, string email, string senhaEsperada, long versaoEsperada, string novaSenha, string evento)
    {
        return await sql.TransacaoAsync(async (conn, tx) =>
        {
            var count = await SqlLeadScopeB2B.ExecutarAsync(conn, tx, """
                UPDATE dbo.Usuarios SET senha_hash=@nova,versao_sessao=versao_sessao+1,data_atualizacao=SYSUTCDATETIME()
                WHERE id=@id AND email=@email AND status='ATIVO'
                  AND senha_hash COLLATE Latin1_General_100_BIN2=@anterior AND versao_sessao=@versao
                """, Int("@id", id), Text("@email", email, 150), Text("@nova", novaSenha, 255), Text("@anterior", senhaEsperada, 255), Long("@versao", versaoEsperada));
            if (count != 1) return false;
            await AuditoriaRepository.RegistrarAsync(conn, tx, id, evento, "Senha atualizada e sessões anteriores revogadas.");
            return true;
        });
    }

    public async Task CriarAdministradorInicialAsync(Usuarios usuario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            // Usuários de leitura podem se cadastrar antes do primeiro administrador.
            _ = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT id FROM dbo.Usuarios WITH (TABLOCKX,HOLDLOCK)", r => r.GetInt32(0));
            var admins = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Select + " WHERE c.nome IN ('Administracao','Admin')", Mapear);
            if (admins.Count > 0) throw new ConflitoException("Já existe um administrador cadastrado. Use a administração de usuários.");
            var profiles = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT p.id FROM dbo.Perfis_acesso p JOIN dbo.Conjuntos_permissoes c ON c.id=p.conjunto_permissoes_id WHERE c.nome IN ('Administracao','Admin') ORDER BY p.id", r => r.GetInt32(0));
            usuario.PerfilAcessoId = profiles.FirstOrDefault();
            if (usuario.PerfilAcessoId == 0) throw new ConflitoException("Cadastre o conjunto Administracao e seu perfil no banco antes de criar o administrador.");
            usuario.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Insert, r => r.GetInt32(0), Parametros(usuario))).Single();
            return true;
        });
    }
}
