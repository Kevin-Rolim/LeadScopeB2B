using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Repositories;

public class LeadRepository : ILeadRepository
{
    private readonly LeadScopeB2BDbContext _context;

    public LeadRepository(LeadScopeB2BDbContext context)
    {
        _context = context;
    }

    public Task<List<PessoasEmpresas>> ListarAsync()
    {
        return _context.PessoasEmpresas
            .AsNoTracking()
            .Include(l => l.Pessoas)
            .Include(l => l.Empresas)
            .Where(l => l.Pessoas.DataDel == null)
            .OrderBy(l => l.Pessoas.Nome)
            .ToListAsync();
    }

    public Task<PessoasEmpresas?> ObterAsync(int pessoaId, int empresaId)
    {
        return _context.PessoasEmpresas
            .Include(l => l.Pessoas)
            .Include(l => l.Empresas)
            .FirstOrDefaultAsync(l => l.PessoaId == pessoaId && l.EmpresaId == empresaId);
    }

    public Task<bool> PessoaExisteAsync(int pessoaId)
    {
        return _context.Pessoas.AnyAsync(p => p.Id == pessoaId && p.DataDel == null);
    }

    public Task<bool> EmpresaExisteAsync(int empresaId)
    {
        return _context.Empresas.AnyAsync(e => e.Id == empresaId);
    }

    public Task<List<Pessoas>> ListarPessoasAsync()
    {
        return _context.Pessoas
            .AsNoTracking()
            .Where(p => p.DataDel == null)
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public Task<List<Empresas>> ListarEmpresasAsync()
    {
        return _context.Empresas
            .AsNoTracking()
            .OrderBy(e => e.RazaoSocial)
            .ToListAsync();
    }

    public async Task AdicionarAsync(PessoasEmpresas lead)
    {
        await _context.PessoasEmpresas.AddAsync(lead);
        await _context.SaveChangesAsync();
    }

    public Task SalvarAlteracoesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
