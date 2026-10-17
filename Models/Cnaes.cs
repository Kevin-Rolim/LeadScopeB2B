using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Cnaes
{
    [Required]
    [StringLength(7, MinimumLength = 7)]
    [RegularExpression(@"^[0-9]{7}$")]
    public string Numero { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Descricao { get; set; } = string.Empty;
}
