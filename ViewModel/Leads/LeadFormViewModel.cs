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

    [StringLength(100)]
    public string? Cargo { get; set; }

    [StringLength(100)]
    public string? Departamento { get; set; }

    [StringLength(50)]
    public string? Senioridade { get; set; }

    [StringLength(50)]
    [Display(Name = "Status do vínculo")]
    public string? StatusVinculo { get; set; }

    [StringLength(50)]
    [Display(Name = "Status do lead")]
    public string? StatusLead { get; set; }

    [Range(0, 100, ErrorMessage = "A confiança deve estar entre 0 e 100.")]
    [Display(Name = "Nível de confiança")]
    public decimal? NivelConfianca { get; set; }

    [StringLength(100)]
    [Display(Name = "Fonte do vínculo")]
    public string? FonteVinculo { get; set; }

    public IReadOnlyList<Models.Pessoas> PessoasDisponiveis { get; set; } = [];
    public IReadOnlyList<Models.Empresas> EmpresasDisponiveis { get; set; } = [];
}
