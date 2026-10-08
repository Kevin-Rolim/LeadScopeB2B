using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Revisoes
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    public string? Decisao { get; set; }

    public string? Comentario { get; set; }

    public DateTime DataHora { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuarios Usuarios { get; set; } = null!;

    [Required]
    public int PessoaId { get; set; }

    [Required]
    public int EmpresaId { get; set; }

    public PessoasEmpresas PessoasEmpresas { get; set; } = null!;
}
