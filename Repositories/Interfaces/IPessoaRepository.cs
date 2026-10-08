namespace LeadScopeB2B.Repositories.Interfaces;

public interface IPessoaRepository
{
    Task<List<Pessoas>> ListarAtivasAsync();
    Task<Pessoas?> ObterAtivaPorIdAsync(int id);
    Task AdicionarAsync(Pessoas pessoa);
    Task SalvarAlteracoesAsync();
}
