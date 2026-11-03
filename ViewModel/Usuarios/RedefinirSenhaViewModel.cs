using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.ViewModels.Usuarios;

public class RedefinirSenhaViewModel
{
    [Required(ErrorMessage="Informe {0}."), StringLength(4096, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.")]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Informe a nova senha."), StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NovaSenha { get; set; } = "";

    [Required(ErrorMessage = "Confirme a nova senha."), Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [DataType(DataType.Password), Display(Name = "Confirmação da nova senha")]
    public string ConfirmacaoSenha { get; set; } = "";
}
