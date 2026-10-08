using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadScopeB2B.Models;

public class PessoasEmpresas
{
    [Required]
    public int PessoaId { get; set; }

    public Pessoas Pessoas { get; set; } = null!;

    [Required]
    public int EmpresaId { get; set; }

    public Empresas Empresas { get; set; } = null!;

    [StringLength(100)]
    public string? Cargo { get; set; }

    [StringLength(100)]
    public string? Departamento { get; set; }

    [StringLength(50)]
    public string? Senioridade { get; set; }

    [StringLength(50)]
    public string? StatusVinculo { get; set; }

    [StringLength(50)]
    public string? StatusLead { get; set; }

    public DateTime? DataRevisao { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal? NivelConfianca { get; set; }

    [StringLength(100)]
    public string? FonteVinculo { get; set; }

    public DateTime? DataVerif { get; set; }

    public ICollection<Revisoes> Revisoes { get; set; } = new List<Revisoes>();

    public ICollection<ExportacoesPessoasEmpresas> ExportacoesPessoasEmpresas { get; set; } = new List<ExportacoesPessoasEmpresas>();
}
