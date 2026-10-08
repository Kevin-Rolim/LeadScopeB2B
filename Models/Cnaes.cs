using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Cnaes
{
    [Key]
    [Required]
    [StringLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Descricao { get; set; } = string.Empty;
}
