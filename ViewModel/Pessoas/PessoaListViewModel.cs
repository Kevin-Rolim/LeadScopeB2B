namespace LeadScopeB2B.ViewModels.Pessoas;

public class PessoaListViewModel
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Status { get; set; }
    public string? Origem { get; set; }
    public bool Bloqueada { get; set; }
}
