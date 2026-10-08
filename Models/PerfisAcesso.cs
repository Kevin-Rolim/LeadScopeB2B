using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class PerfisAcesso
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    public int ConjuntoPermissoesId { get; set; }

    public ConjuntoPermissoes ConjuntoPermissoes { get; set; } = null!;
}
