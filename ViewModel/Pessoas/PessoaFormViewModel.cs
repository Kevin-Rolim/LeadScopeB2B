using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Pessoas;

public class PessoaFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string Nome { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Telefone { get; set; }

    [Url(ErrorMessage = "Informe uma URL válida.")]
    [StringLength(255, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "LinkedIn")]
    public string? UrlLinkedIn { get; set; }

    [Required(ErrorMessage = "Informe o status.")]
    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [RegularExpression("^(PENDENTE|VALIDADO)$", ErrorMessage="O campo {0} possui formato inválido.") ]
    public string Status { get; set; } = "PENDENTE";

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Origem { get; set; }
}
