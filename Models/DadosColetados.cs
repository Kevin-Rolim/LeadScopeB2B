using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadScopeB2B.Models;

public class DadosColetados
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Campo { get; set; } = string.Empty;

    [Required]
    public string Valor { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal? NivelConfianca { get; set; }

    [Required]
    public int FonteDadosID { get; set; }

    public FonteDados FonteDados { get; set; } = null!;

    public int? PessoaId { get; set; }

    public Pessoas? Pessoas { get; set; }

    public int? EmpresaId { get; set; }

    public Empresas? Empresa { get; set; }
}
