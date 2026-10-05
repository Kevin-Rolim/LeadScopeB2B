public class Pessoas
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string UrlLinkedin { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public DateTime DataColeta { get; set; }
    public DateTime DataBloqueio { get; set; }
    public DateTime DataDel { get; set; }
}