using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class AuditoriaService(AuditoriaRepository repository)
{
    public Task<List<Logs>> ListarAsync() => repository.ListarAsync();
    public Task RegistrarAsync(int usuarioId, string evento, string descricao) => repository.RegistrarAsync(usuarioId, evento, descricao);
}
