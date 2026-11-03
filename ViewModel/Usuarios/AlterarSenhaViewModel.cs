using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Usuarios;

public class AlterarSenhaViewModel
{
    [Required(ErrorMessage = "Informe a senha atual."), StringLength(100)]
    [DataType(DataType.Password), Display(Name = "Senha atual")]
    public string SenhaAtual { get; set; } = "";

    [Required(ErrorMessage = "Informe a nova senha."), StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NovaSenha { get; set; } = "";

    [Required(ErrorMessage = "Confirme a nova senha."), Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
    [StringLength(100)]
    [DataType(DataType.Password), Display(Name = "Confirmação da nova senha")]
    public string ConfirmacaoSenha { get; set; } = "";
}
