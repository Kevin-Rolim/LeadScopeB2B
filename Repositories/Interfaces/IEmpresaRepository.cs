namespace LeadScopeB2B.Repositories.Interfaces;

public interface IEmpresaRepository
{
    Task<List<Empresas>> ListarAsync();
    Task<Empresas?> ObterPorIdAsync(int id);
    Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId = null);
    Task AdicionarAsync(Empresas empresa);
    Task SalvarAlteracoesAsync();
}
