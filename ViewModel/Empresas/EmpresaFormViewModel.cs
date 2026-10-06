using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Empresas;

public class EmpresaFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe a razão social.")]
    [StringLength(200)]
    [Display(Name = "Razão social")]
    public string RazaoSocial { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Nome fantasia")]
    public string? NomeFantasia { get; set; }

    [StringLength(18)]
    [Display(Name = "CNPJ")]
    public string? Cnpj { get; set; }

    [Url(ErrorMessage = "Informe uma URL válida.")]
    [StringLength(255)]
    [Display(Name = "Site")]
    public string? UrlSite { get; set; }

    [StringLength(100)]
    public string? Segmento { get; set; }

    [StringLength(50)]
    public string? Porte { get; set; }

    [StringLength(100)]
    public string? Cidade { get; set; }

    [StringLength(2, MinimumLength = 2, ErrorMessage = "Use a sigla do estado com 2 letras.")]
    public string? Estado { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Informe o status.")]
    [StringLength(50)]
    public string Status { get; set; } = "PENDENTE";
}
