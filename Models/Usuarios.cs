using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Usuarios
{
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
    [System.Text.Json.Serialization.JsonIgnore]
    public string Senha { get; set; } = string.Empty;

    [Required]
    public int PerfilAcessoId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public long VersaoSessao { get; set; }

    public string Status { get; set; } = "ATIVO";

    public PerfisAcesso PerfisAcesso { get; set; } = null!;
}
