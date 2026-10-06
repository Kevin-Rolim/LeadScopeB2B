using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly LeadScopeB2BDbContext _context;

    public UsuarioRepository(LeadScopeB2BDbContext context)
    {
        _context = context;
    }

    public Task<List<Usuarios>> ListarAsync()
    {
        return _context.Usuarios
            .AsNoTracking()
            .Include(u => u.PerfisAcesso)
            .OrderBy(u => u.Nome)
            .ToListAsync();
    }

    public Task<Usuarios?> ObterPorIdAsync(int id)
    {
        return _context.Usuarios
            .Include(u => u.PerfisAcesso)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public Task<bool> EmailExisteAsync(string email, int? ignorarId = null)
    {
        return _context.Usuarios.AnyAsync(u =>
            u.Email == email && (!ignorarId.HasValue || u.Id != ignorarId.Value));
    }

    public Task<List<PerfisAcesso>> ListarPerfisAsync()
    {
        return _context.PerfisAcesso
            .AsNoTracking()
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public Task<bool> PerfilExisteAsync(int perfilId)
    {
        return _context.PerfisAcesso.AnyAsync(p => p.Id == perfilId);
    }

    public async Task AdicionarAsync(Usuarios usuario)
    {
        await _context.Usuarios.AddAsync(usuario);
        await _context.SaveChangesAsync();
    }

    public Task SalvarAlteracoesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
