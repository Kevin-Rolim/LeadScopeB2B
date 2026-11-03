using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.ViewModels.Usuarios;

public class LoginViewModel
{
    [Required(ErrorMessage="Informe {0}."), EmailAddress(ErrorMessage="Informe um e-mail válido."), StringLength(150, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage="Informe {0}."), StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres."), DataType(DataType.Password)]
    public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
