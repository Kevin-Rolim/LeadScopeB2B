using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class FonteDados
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Tipo { get; set; }

    [StringLength(255)]
    public string? Url { get; set; }

    [Range(0, 100)]
    public decimal? Confiabilidade { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataHoraMod { get; set; }

    public DateTime? DataHoraDel { get; set; }
}
