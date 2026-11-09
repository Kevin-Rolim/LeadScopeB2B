using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Empresas;

public class EmpresaFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe a razão social.")]
    [StringLength(200, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Razão social")]
    public string RazaoSocial { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Nome fantasia")]
    public string? NomeFantasia { get; set; }

    [StringLength(18, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "CNPJ")]
    public string? Cnpj { get; set; }

    [Url(ErrorMessage = "Informe uma URL válida.")]
    [StringLength(255, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Site")]
    public string? UrlSite { get; set; }

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Segmento { get; set; }

    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Porte { get; set; }

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Cidade { get; set; }

    [StringLength(2, MinimumLength = 2, ErrorMessage = "Use a sigla do estado com 2 letras.")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use a sigla do estado.")]
    public string? Estado { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Informe o status.")]
    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [RegularExpression("^(PENDENTE|VALIDADA|INATIVA)$", ErrorMessage="O campo {0} possui formato inválido.") ]
    public string Status { get; set; } = "PENDENTE";
}
