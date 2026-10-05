namespace LeadScopeB2B.ViewModels.Usuarios;
public class UsuarioEditViewModel
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int PerfilAcessoId { get; set; }
    public PerfisAcesso? PerfisAcesso { get; set; }
}