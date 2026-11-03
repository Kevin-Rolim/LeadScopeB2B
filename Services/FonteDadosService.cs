using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class FonteDadosService(FonteDadosRepository repository)
{
    public Task<List<FonteDados>> ListarAsync() => repository.ListarAsync();
    public Task<bool> EstaAtivaAsync(int id) => repository.EstaAtivaAsync(id);
    public Task CadastrarAsync(FonteDados model)
    {
        CadastroRules.Validar(model);
        model.Nome = model.Nome.Trim();
        model.Tipo = CadastroRules.Estado(model.Tipo, "API", "ARQUIVO", "SITE", "MANUAL");
        model.Url = CadastroRules.Url(model.Url);
        return repository.AdicionarAsync(model);
    }
    public Task AlterarAtivoAsync(int id, bool ativo) => repository.AlterarAtivoAsync(id, ativo);
}
