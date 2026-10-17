using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class DadosColetados
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Campo { get; set; } = string.Empty;

    [Required]
    public string Valor { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal? NivelConfianca { get; set; }

    public string StatusValidacao { get; set; } = "PENDENTE";
    public string? ValorNormalizado { get; set; }
    public string? EvidenciaUrl { get; set; }
    public DateTime DataColeta { get; set; }

    [Required]
    public int FonteDadosID { get; set; }

    public FonteDados FonteDados { get; set; } = null!;

    public int? PessoaId { get; set; }

    public Pessoas? Pessoas { get; set; }

    public int? EmpresaId { get; set; }

    public Empresas? Empresa { get; set; }
}
