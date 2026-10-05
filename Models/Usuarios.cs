using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Usuarios
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Senha { get; set; } = string.Empty;

    [Required]
    public int PerfilAcessoId { get; set; }

    public PerfisAcesso PerfisAcesso { get; set; } = null!;
}
