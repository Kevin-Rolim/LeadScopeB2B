using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.ViewModels.Usuarios;

public class RecuperarAcessoViewModel
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."), StringLength(150)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";
}
