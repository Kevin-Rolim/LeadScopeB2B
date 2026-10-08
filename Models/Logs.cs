using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Logs
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string TiporEvento { get; set; } = string.Empty;

    public string? DescricaoEvento { get; set; }

    public DateTime DataHora { get; set; }

    public string? DadosAlterados { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuarios Usuarios { get; set; } = null!;
}
