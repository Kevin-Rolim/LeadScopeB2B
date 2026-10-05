using LeadScopeB2B.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadScopeB2B.Data;

public class LeadScopeB2BDbContext : DbContext
{
    public LeadScopeB2BDbContext(DbContextOptions<LeadScopeB2BDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CnaesEmpresas>()
            .HasKey(ce => new { ce.CnaeNumero, ce.EmpresaId });
        modelBuilder.Entity<CnaesEmpresas>()
            .HasOne(ce => ce.Cnaes)
            .WithMany()
            .HasForeignKey(ce => ce.CnaeNumero);
        modelBuilder.Entity<CnaesEmpresas>()
            .HasOne(ce => ce.Empresas)
            .WithMany()
            .HasForeignKey(ce => ce.EmpresaId);

        modelBuilder.Entity<PessoasEmpresas>()
            .HasKey(pe => new { pe.PessoaId, pe.EmpresaId });
        modelBuilder.Entity<PessoasEmpresas>()
            .HasOne(pe => pe.Pessoas)
            .WithMany()
            .HasForeignKey(pe => pe.PessoaId);
        modelBuilder.Entity<PessoasEmpresas>()
            .HasOne(pe => pe.Empresas)
            .WithMany()
            .HasForeignKey(pe => pe.EmpresaId);

        modelBuilder.Entity<ExportacoesPessoasEmpresas>()
            .HasKey(epe => new { epe.ExportacoesId, epe.PessoaId, epe.EmpresaId });
        modelBuilder.Entity<ExportacoesPessoasEmpresas>()
            .HasOne(epe => epe.Exportacoes)
            .WithMany()
            .HasForeignKey(epe => epe.ExportacoesId);
        modelBuilder.Entity<ExportacoesPessoasEmpresas>()
            .HasOne(epe => epe.PessoasEmpresas)
            .WithMany(pe => pe.ExportacoesPessoasEmpresas)
            .HasForeignKey(epe => new { epe.PessoaId, epe.EmpresaId });

        modelBuilder.Entity<Revisoes>()
            .HasOne(r => r.PessoasEmpresas)
            .WithMany(pe => pe.Revisoes)
            .HasForeignKey(r => new { r.PessoaId, r.EmpresaId });

        modelBuilder.Entity<DadosColetados>()
            .HasOne(dc => dc.Pessoas)
            .WithMany()
            .HasForeignKey(dc => dc.PessoaId);
        modelBuilder.Entity<DadosColetados>()
            .HasOne(dc => dc.FonteDados)
            .WithMany()
            .HasForeignKey(dc => dc.FonteDadosID);
        modelBuilder.Entity<DadosColetados>()
            .HasOne(dc => dc.Empresa)
            .WithMany()
            .HasForeignKey(dc => dc.EmpresaId);
        modelBuilder.Entity<DadosColetados>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_DadosColetados_EntidadeDestino",
                "([PessoaId] IS NOT NULL AND [EmpresaId] IS NULL) OR ([PessoaId] IS NULL AND [EmpresaId] IS NOT NULL)"));

        modelBuilder.Entity<PerfisAcesso>()
            .HasOne(p => p.ConjuntoPermissoes)
            .WithMany()
            .HasForeignKey(p => p.ConjuntoPermissoesId);

        modelBuilder.Entity<Usuarios>()
            .HasOne(u => u.PerfisAcesso)
            .WithMany()
            .HasForeignKey(u => u.PerfilAcessoId);

        modelBuilder.Entity<Logs>()
            .HasOne(l => l.Usuarios)
            .WithMany()
            .HasForeignKey(l => l.UsuarioId);

        modelBuilder.Entity<Revisoes>()
            .HasOne(r => r.Usuarios)
            .WithMany()
            .HasForeignKey(r => r.UsuarioId);

        modelBuilder.Entity<Exportacoes>()
            .HasOne(e => e.Usuarios)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId);

        modelBuilder.Entity<Usuarios>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Empresas>().HasIndex(e => e.Cnpj).IsUnique();

        modelBuilder.Entity<FonteDados>().Property(f => f.DataHoraMod).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Pessoas>().Property(p => p.DataColeta).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Empresas>().Property(e => e.DataCriacao).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Empresas>().Property(e => e.DataAtualizacao).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Logs>().Property(l => l.DataHora).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Revisoes>().Property(r => r.DataHora).HasDefaultValueSql("SYSDATETIME()");
        modelBuilder.Entity<Exportacoes>().Property(e => e.DataHora).HasDefaultValueSql("SYSDATETIME()");
    }

    public DbSet<Cnaes> Cnaes { get; set; } = null!;
    public DbSet<CnaesEmpresas> CnaesEmpresas { get; set; } = null!;
    public DbSet<ConjuntoPermissoes> ConjuntoPermissoes { get; set; } = null!;
    public DbSet<DadosColetados> DadosColetados { get; set; } = null!;
    public DbSet<Empresas> Empresas { get; set; } = null!;
    public DbSet<Exportacoes> Exportacoes { get; set; } = null!;
    public DbSet<ExportacoesPessoasEmpresas> ExportacoesPessoasEmpresas { get; set; } = null!;
    public DbSet<FonteDados> FonteDados { get; set; } = null!;
    public DbSet<Logs> Logs { get; set; } = null!;
    public DbSet<PerfisAcesso> PerfisAcesso { get; set; } = null!;
    public DbSet<Pessoas> Pessoas { get; set; } = null!;
    public DbSet<PessoasEmpresas> PessoasEmpresas { get; set; } = null!;
    public DbSet<Revisoes> Revisoes { get; set; } = null!;
    public DbSet<Usuarios> Usuarios { get; set; } = null!;
}
