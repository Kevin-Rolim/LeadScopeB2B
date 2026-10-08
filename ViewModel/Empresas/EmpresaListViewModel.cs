namespace LeadScopeB2B.ViewModels.Empresas;

public class EmpresaListViewModel
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? Cnpj { get; set; }
    public string? Segmento { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Status { get; set; }
}
