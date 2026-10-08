namespace LeadScopeB2B.ViewModels.Leads;

public class LeadListViewModel
{
    public int PessoaId { get; set; }
    public int EmpresaId { get; set; }
    public string Pessoa { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? Senioridade { get; set; }
    public string? StatusLead { get; set; }
    public decimal? NivelConfianca { get; set; }
}
