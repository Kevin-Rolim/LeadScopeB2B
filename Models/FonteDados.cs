using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadScopeB2B.Models;

public class FonteDados
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Tipo { get; set; }

    [StringLength(255)]
    public string? Url { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal? Confiabilidade { get; set; }

    public DateTime DataHoraMod { get; set; }

    public DateTime? DataHoraDel { get; set; }
}
