public class PerfisAcesso
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int ConjuntoPermissoesId { get; set; }
    public ConjuntoPermissoes? ConjuntoPermissoes { get; set; }
}