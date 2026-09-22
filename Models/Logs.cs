public class Logs
{
    public int Id { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public string? DescEvento { get; set; }
    public DateTime DataHora { get; set; }
    public string? DadosAlt { get; set; }
    public int UsuarioId { get; set; }
}