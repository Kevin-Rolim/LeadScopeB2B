using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Exportacoes
{
    [Key]
    public int Id { get; set; }

    [Range(0, int.MaxValue)]
    public int? QtdItens { get; set; }

    public DateTime DataHora { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuarios Usuarios { get; set; } = null!;
}
