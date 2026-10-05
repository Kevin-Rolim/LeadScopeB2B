using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class Empresas
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    [StringLength(200)]
    public string? NomeFantasia { get; set; }

    [StringLength(18)]
    public string? Cnpj { get; set; }

    [StringLength(255)]
    public string? UrlSite { get; set; }

    [StringLength(100)]
    public string? Segmento { get; set; }

    [StringLength(50)]
    public string? Porte { get; set; }

    [StringLength(100)]
    public string? Cidade { get; set; }

    [StringLength(2, MinimumLength = 2)]
    public string? Estado { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Telefone { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime DataAtualizacao { get; set; }
}
