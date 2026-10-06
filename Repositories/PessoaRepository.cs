using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Repositories;

public class PessoaRepository : IPessoaRepository
{
    private readonly LeadScopeB2BDbContext _context;

    public PessoaRepository(LeadScopeB2BDbContext context)
    {
        _context = context;
    }

    public Task<List<Pessoas>> ListarAtivasAsync()
    {
        return _context.Pessoas
            .AsNoTracking()
            .Where(p => p.DataDel == null)
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public Task<Pessoas?> ObterAtivaPorIdAsync(int id)
    {
        return _context.Pessoas.FirstOrDefaultAsync(p => p.Id == id && p.DataDel == null);
    }

    public async Task AdicionarAsync(Pessoas pessoa)
    {
        await _context.Pessoas.AddAsync(pessoa);
        await _context.SaveChangesAsync();
    }

    public Task SalvarAlteracoesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
