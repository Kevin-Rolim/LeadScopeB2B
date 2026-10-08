using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Pessoas
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Telefone { get; set; }

    [StringLength(255)]
    public string? UrlLinkedin { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(100)]
    public string? Origem { get; set; }

    public DateTime DataColeta { get; set; }

    public DateTime? DataBloqueio { get; set; }

    public DateTime? DataDel { get; set; }
}
