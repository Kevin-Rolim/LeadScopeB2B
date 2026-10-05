using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Data;
public class LeadScopeB2BDbContext: DbContext
{
    public LeadScopeB2BDbContext(DbContextOptions<LeadScopeB2BDbContext> options):base(options){}
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    base.OnModelCreating(modelBuilder);
    modelBuilder.Entity<PessoasEmpresas>()
        .HasKey(m => new { m.PessoaId, m.EmpresaId });
    modelBuilder.Entity<CnaesEmpresas>()
        .HasKey(h => new { h.CnaeNumero, h.EmpresaId });
    modelBuilder.Entity<ExportacoesPessoasEmpresas>()
        .HasKey(c => new { c.ExportacoesId, c.PessoaId, c.EmpresaId });
    modelBuilder.Entity<ExportacoesPessoasEmpresas>()
        .HasOne(epe => epe.PessoasEmpresas)
        .WithMany()
        .HasForeignKey(epe => new { epe.PessoaId, epe.EmpresaId });
    }
    
    public DbSet<Usuarios> Usuarios {get;set;}
    public DbSet<ConjuntoPermissoes> ConjuntoPermissoes {get;set;}
    public DbSet<DadosColetados> DadosColetados {get;set;}
    public DbSet<FonteDados> FonteDados {get;set;}
    public DbSet<Logs> Logs {get;set;}
    public DbSet<PerfisAcesso> PerfisAcesso {get;set;}
    public DbSet<Revisoes> Revisoes {get;set;}
}