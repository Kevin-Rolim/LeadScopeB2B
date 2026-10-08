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

        MapearTabelasEColunas(modelBuilder);

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
            .ToTable("Dados_coletados", table => table.HasCheckConstraint(
                "CK_DadosColetados_EntidadeDestino",
                "([pessoa_id] IS NOT NULL AND [empresa_id] IS NULL) OR ([pessoa_id] IS NULL AND [empresa_id] IS NOT NULL)"));

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

        modelBuilder.Entity<FonteDados>().Property(f => f.DataHoraMod).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Pessoas>().Property(p => p.DataColeta).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Pessoas>().Property(p => p.Status).HasDefaultValue("PENDENTE");
        modelBuilder.Entity<Empresas>().Property(e => e.DataCriacao).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Empresas>().Property(e => e.DataAtualizacao).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Empresas>().Property(e => e.Status).HasDefaultValue("PENDENTE");
        modelBuilder.Entity<Logs>().Property(l => l.DataHora).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Revisoes>().Property(r => r.DataHora).HasDefaultValueSql("SYSUTCDATETIME()");
        modelBuilder.Entity<Exportacoes>().Property(e => e.DataHora).HasDefaultValueSql("SYSUTCDATETIME()");
    }

    private static void MapearTabelasEColunas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cnaes>(entity =>
        {
            entity.ToTable("CNAEs");
        });

        modelBuilder.Entity<CnaesEmpresas>(entity =>
        {
            entity.ToTable("CNAEs_Empresas");
        });

        modelBuilder.Entity<ConjuntoPermissoes>(entity =>
        {
            entity.ToTable("Conjuntos_permissoes");
        });

        modelBuilder.Entity<DadosColetados>(entity =>
        {
            entity.ToTable("Dados_coletados");
        });

        modelBuilder.Entity<Empresas>(entity =>
        {
            entity.ToTable("Empresas");
        });

        modelBuilder.Entity<Exportacoes>(entity =>
        {
            entity.ToTable("Exportacoes");
        });

        modelBuilder.Entity<ExportacoesPessoasEmpresas>(entity =>
        {
            entity.ToTable("Exportacoes_Pessoas_Empresas");
        });

        modelBuilder.Entity<FonteDados>(entity =>
        {
            entity.ToTable("Fontes_dados");
        });

        modelBuilder.Entity<Logs>(entity =>
        {
            entity.ToTable("Logs");
        });

        modelBuilder.Entity<PerfisAcesso>(entity =>
        {
            entity.ToTable("Perfis_acesso");
        });

        modelBuilder.Entity<Pessoas>(entity =>
        {
            entity.ToTable("Pessoas");
        });

        modelBuilder.Entity<PessoasEmpresas>(entity =>
        {
            entity.ToTable("Pessoas_Empresas");
        });

        modelBuilder.Entity<Revisoes>(entity =>
        {
            entity.ToTable("Revisoes");
        });

        modelBuilder.Entity<Usuarios>(entity =>
        {
            entity.ToTable("Usuarios");
        });
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
