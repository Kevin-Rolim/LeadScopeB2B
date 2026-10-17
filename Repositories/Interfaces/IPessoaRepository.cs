namespace LeadScopeB2B.Repositories.Interfaces;

public interface IPessoaRepository
{
    Task<List<Pessoas>> ListarAtivasAsync(string? busca = null, string? status = null);
    Task<Pessoas?> ObterAtivaPorIdAsync(int id);
    Task AdicionarAsync(Pessoas pessoa);
    Task AtualizarAsync(Pessoas pessoa);
    Task<bool> DuplicadaAsync(string? email, string? linkedin, int? ignorarId = null);
}
