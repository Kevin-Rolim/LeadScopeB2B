using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.ViewModels.Leads;

public class LeadFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma pessoa.")]
    [Display(Name = "Pessoa")]
    public int PessoaId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma empresa.")]
    [Display(Name = "Empresa")]
    public int EmpresaId { get; set; }

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Cargo { get; set; }

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Departamento { get; set; }

    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    public string? Senioridade { get; set; }

    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Status do vínculo")]
    public string? StatusVinculo { get; set; } = "INCERTO";

    [StringLength(50, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Status do lead")]
    public string? StatusLead { get; set; } = "NOVO";

    [Range(0, 100, ErrorMessage = "A confiança deve estar entre 0 e 100.")]
    [Display(Name = "Nível de confiança")]
    public decimal? NivelConfianca { get; set; }

    [StringLength(100, ErrorMessage="O campo {0} deve ter no máximo {1} caracteres.") ]
    [Display(Name = "Fonte do vínculo")]
    public string? FonteVinculo { get; set; }

    public IReadOnlyList<Models.Pessoas> PessoasDisponiveis { get; set; } = [];
    public IReadOnlyList<Models.Empresas> EmpresasDisponiveis { get; set; } = [];
}
