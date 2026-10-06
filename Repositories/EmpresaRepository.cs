using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Repositories;

public class EmpresaRepository : IEmpresaRepository
{
    private readonly LeadScopeB2BDbContext _context;

    public EmpresaRepository(LeadScopeB2BDbContext context)
    {
        _context = context;
    }

    public Task<List<Empresas>> ListarAsync()
    {
        return _context.Empresas
            .AsNoTracking()
            .OrderBy(e => e.RazaoSocial)
            .ToListAsync();
    }

    public Task<Empresas?> ObterPorIdAsync(int id)
    {
        return _context.Empresas.FirstOrDefaultAsync(e => e.Id == id);
    }

    public Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId = null)
    {
        return _context.Empresas.AnyAsync(e =>
            e.Cnpj == cnpj && (!ignorarId.HasValue || e.Id != ignorarId.Value));
    }

    public async Task AdicionarAsync(Empresas empresa)
    {
        await _context.Empresas.AddAsync(empresa);
        await _context.SaveChangesAsync();
    }

    public Task SalvarAlteracoesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
