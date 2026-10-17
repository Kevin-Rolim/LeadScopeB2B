namespace LeadScopeB2B.ViewModels.Empresas;

public class EmpresaDetailsViewModel : EmpresaFormViewModel
{
    public IReadOnlyList<Models.CnaesEmpresas> Cnaes { get; set; } = [];
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
