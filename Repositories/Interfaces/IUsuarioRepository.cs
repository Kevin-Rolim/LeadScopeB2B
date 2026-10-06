namespace LeadScopeB2B.Repositories.Interfaces;

public interface IUsuarioRepository
{
    Task<List<Usuarios>> ListarAsync();
    Task<Usuarios?> ObterPorIdAsync(int id);
    Task<bool> EmailExisteAsync(string email, int? ignorarId = null);
    Task<List<PerfisAcesso>> ListarPerfisAsync();
    Task<bool> PerfilExisteAsync(int perfilId);
    Task AdicionarAsync(Usuarios usuario);
    Task SalvarAlteracoesAsync();
}
