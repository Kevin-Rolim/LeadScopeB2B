using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.ViewModels.Usuarios;

public class LoginViewModel
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";
    [Required, StringLength(100), DataType(DataType.Password)]
    public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
