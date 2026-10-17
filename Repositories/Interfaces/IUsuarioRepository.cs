namespace LeadScopeB2B.Repositories.Interfaces;

public interface IUsuarioRepository
{
    Task<List<Usuarios>> ListarAsync();
    Task<Usuarios?> ObterPorIdAsync(int id);
    Task<bool> EmailExisteAsync(string email, int? ignorarId = null);
    Task<List<PerfisAcesso>> ListarPerfisAsync();
    Task<bool> PerfilExisteAsync(int perfilId);
    Task AdicionarAsync(Usuarios usuario);
    Task AtualizarAsync(Usuarios usuario);
    Task<Usuarios?> ObterPorEmailAsync(string email);
    Task CadastrarPublicoAsync(Usuarios usuario);
    Task<Usuarios> AtualizarPerfilAsync(int id, string nome, string email, string senhaEsperada, long versaoEsperada);
    Task<bool> AtualizarSenhaAsync(int id, string email, string senhaEsperada, long versaoEsperada, string novaSenha, string evento);
    Task CriarAdministradorInicialAsync(Usuarios usuario);
}
