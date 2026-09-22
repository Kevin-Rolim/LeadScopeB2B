public class Pessoas
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? UrlLinkedin { get; set; }
    public string? Status { get; set; }
    public string? Origem { get; set; }
    public DateTime DataColeta { get; set; }
    public DateTime? DataBloqueio { get; set; }
    public DateTime? DataDel { get; set; }
}