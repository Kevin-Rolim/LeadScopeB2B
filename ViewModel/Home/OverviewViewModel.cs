namespace LeadScopeB2B.ViewModels.Home;
public class OverviewViewModel
{
    public List<EmpresaListViewModel> Empresas { get; init; } = [];
    public List<PessoaListViewModel> Pessoas { get; init; } = [];
    public List<LeadListViewModel> Leads { get; init; } = [];
}
