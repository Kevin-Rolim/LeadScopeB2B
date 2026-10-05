using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class ConjuntoPermissoes
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Descricao { get; set; }
}
