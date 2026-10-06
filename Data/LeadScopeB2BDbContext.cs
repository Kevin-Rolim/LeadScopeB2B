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
            entity.Property(e => e.Numero).HasColumnName("numero");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
        });

        modelBuilder.Entity<CnaesEmpresas>(entity =>
        {
            entity.ToTable("CNAEs_Empresas");
            entity.Property(e => e.CnaeNumero).HasColumnName("cnae_numero");
            entity.Property(e => e.EmpresaId).HasColumnName("empresa_id");
        });

        modelBuilder.Entity<ConjuntoPermissoes>(entity =>
        {
            entity.ToTable("Conjuntos_permissoes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
        });

        modelBuilder.Entity<DadosColetados>(entity =>
        {
            entity.ToTable("Dados_coletados");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Campo).HasColumnName("campo");
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.NivelConfianca).HasColumnName("nivel_confianca");
            entity.Property(e => e.FonteDadosID).HasColumnName("fonte_dados_id");
            entity.Property(e => e.PessoaId).HasColumnName("pessoa_id");
            entity.Property(e => e.EmpresaId).HasColumnName("empresa_id");
        });

        modelBuilder.Entity<Empresas>(entity =>
        {
            entity.ToTable("Empresas");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RazaoSocial).HasColumnName("razao_social");
            entity.Property(e => e.NomeFantasia).HasColumnName("nome_fantasia");
            entity.Property(e => e.Cnpj).HasColumnName("cnpj");
            entity.Property(e => e.UrlSite).HasColumnName("site");
            entity.Property(e => e.Segmento).HasColumnName("segmento");
            entity.Property(e => e.Porte).HasColumnName("porte");
            entity.Property(e => e.Cidade).HasColumnName("cidade");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.Email).HasColumnName("email_institucional");
            entity.Property(e => e.Telefone).HasColumnName("telefone_comercial");
            entity.Property(e => e.Status).HasColumnName("status_validacao");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataAtualizacao).HasColumnName("data_atualizacao");
        });

        modelBuilder.Entity<Exportacoes>(entity =>
        {
            entity.ToTable("Exportacoes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.QtdItens).HasColumnName("qntd_itens");
            entity.Property(e => e.DataHora).HasColumnName("data_hora");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
        });

        modelBuilder.Entity<ExportacoesPessoasEmpresas>(entity =>
        {
            entity.ToTable("Exportacoes_Pessoas_Empresas");
            entity.Property(e => e.ExportacoesId).HasColumnName("exportacao_id");
            entity.Property(e => e.PessoaId).HasColumnName("pessoa_id");
            entity.Property(e => e.EmpresaId).HasColumnName("empresa_id");
        });

        modelBuilder.Entity<FonteDados>(entity =>
        {
            entity.ToTable("Fontes_dados");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Tipo).HasColumnName("tipo");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.Confiabilidade).HasColumnName("confiabilidade");
            entity.Property(e => e.DataHoraMod).HasColumnName("data_hora_mod");
            entity.Property(e => e.DataHoraDel).HasColumnName("data_hora_del");
        });

        modelBuilder.Entity<Logs>(entity =>
        {
            entity.ToTable("Logs");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TiporEvento).HasColumnName("tipo_evento");
            entity.Property(e => e.DescricaoEvento).HasColumnName("desc_evento");
            entity.Property(e => e.DataHora).HasColumnName("data_hora");
            entity.Property(e => e.DadosAlterados).HasColumnName("dados_alt");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
        });

        modelBuilder.Entity<PerfisAcesso>(entity =>
        {
            entity.ToTable("Perfis_acesso");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.ConjuntoPermissoesId).HasColumnName("conjunto_permissoes_id");
        });

        modelBuilder.Entity<Pessoas>(entity =>
        {
            entity.ToTable("Pessoas");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Email).HasColumnName("email_profissional");
            entity.Property(e => e.Telefone).HasColumnName("telefone_comercial");
            entity.Property(e => e.UrlLinkedin).HasColumnName("url_perfil_profissional");
            entity.Property(e => e.Status).HasColumnName("status_validacao");
            entity.Property(e => e.Origem).HasColumnName("origem");
            entity.Property(e => e.DataColeta).HasColumnName("data_coleta");
            entity.Property(e => e.DataBloqueio).HasColumnName("data_bloqueio");
            entity.Property(e => e.DataDel).HasColumnName("data_del");
        });

        modelBuilder.Entity<PessoasEmpresas>(entity =>
        {
            entity.ToTable("Pessoas_Empresas");
            entity.Property(e => e.PessoaId).HasColumnName("pessoa_id");
            entity.Property(e => e.EmpresaId).HasColumnName("empresa_id");
            entity.Property(e => e.Cargo).HasColumnName("cargo");
            entity.Property(e => e.Departamento).HasColumnName("departamento");
            entity.Property(e => e.Senioridade).HasColumnName("senioridade");
            entity.Property(e => e.StatusVinculo).HasColumnName("status_vinculo");
            entity.Property(e => e.StatusLead).HasColumnName("status_lead");
            entity.Property(e => e.DataRevisao).HasColumnName("data_revisao");
            entity.Property(e => e.NivelConfianca).HasColumnName("nivel_confianca");
            entity.Property(e => e.FonteVinculo).HasColumnName("fonte_vinculo");
            entity.Property(e => e.DataVerif).HasColumnName("data_verificacao");
        });

        modelBuilder.Entity<Revisoes>(entity =>
        {
            entity.ToTable("Revisoes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Decisao).HasColumnName("decisao");
            entity.Property(e => e.Comentario).HasColumnName("comentario");
            entity.Property(e => e.DataHora).HasColumnName("data_hora");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.PessoaId).HasColumnName("pessoa_id");
            entity.Property(e => e.EmpresaId).HasColumnName("empresa_id");
        });

        modelBuilder.Entity<Usuarios>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Senha).HasColumnName("senha_hash");
            entity.Property(e => e.PerfilAcessoId).HasColumnName("perfil_acesso_id");
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
