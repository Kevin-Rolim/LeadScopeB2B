public class Logs
{
    public int Id { get; set; }
    public string TiporEvento { get; set; } = string.Empty;
    public string DescricaoEvento { get; set; } = string.Empty;
    public DateTime DataHora { get; set; }
    public string DadosAlterados { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public Usuarios? Usuarios { get; set; }
}