using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Usuarios;

public class UsuarioCreateViewModel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(100)]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um perfil de acesso.")]
    [Display(Name = "Perfil de acesso")]
    public int PerfilAcessoId { get; set; }

    public IReadOnlyList<PerfisAcesso> PerfisDisponiveis { get; set; } = [];
}
