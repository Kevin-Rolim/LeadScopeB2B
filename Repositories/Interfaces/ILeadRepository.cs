namespace LeadScopeB2B.Repositories.Interfaces;

public interface ILeadRepository
{
    Task<List<PessoasEmpresas>> ListarAsync();
    Task<PessoasEmpresas?> ObterAsync(int pessoaId, int empresaId);
    Task<bool> PessoaExisteAsync(int pessoaId);
    Task<bool> EmpresaExisteAsync(int empresaId);
    Task<List<Pessoas>> ListarPessoasAsync();
    Task<List<Empresas>> ListarEmpresasAsync();
    Task AdicionarAsync(PessoasEmpresas lead);
    Task SalvarAlteracoesAsync();
}
