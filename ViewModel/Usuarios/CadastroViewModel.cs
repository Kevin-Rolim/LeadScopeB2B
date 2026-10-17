using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Usuarios;

public class CadastroViewModel
{
    [Required(ErrorMessage = "Informe o nome."), StringLength(100)]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."), StringLength(150)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha."), StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha."), Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem.")]
    [StringLength(100)]
    [DataType(DataType.Password), Display(Name = "Confirmação de senha")]
    public string ConfirmacaoSenha { get; set; } = "";
}
