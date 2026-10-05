public class Exportacoes
{
    public int Id { get; set; }
    public int QtdItens { get; set; }
    public DateTime DataHora { get; set; }
    public int UsuarioId { get; set; }
    public Usuarios? Usuarios { get; set; }
}