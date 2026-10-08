using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Pessoas;

public class PessoaFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Telefone { get; set; }

    [Url(ErrorMessage = "Informe uma URL válida.")]
    [StringLength(255)]
    [Display(Name = "LinkedIn")]
    public string? UrlLinkedin { get; set; }

    [Required(ErrorMessage = "Informe o status.")]
    [StringLength(50)]
    public string Status { get; set; } = "PENDENTE";

    [StringLength(100)]
    public string? Origem { get; set; }
}
