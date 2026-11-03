using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LeadScopeB2B.ViewModels.Usuarios;

public class PerfilViewModel
{
    [Required(ErrorMessage = "Informe o nome."), StringLength(100)]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."), StringLength(150)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe sua senha atual para confirmar a alteração."), StringLength(100)]
    [DataType(DataType.Password), Display(Name = "Senha atual")]
    public string SenhaAtual { get; set; } = "";

    [BindNever]
    public string? PerfilAcesso { get; set; }
}
