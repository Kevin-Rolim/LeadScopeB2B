public class Revisoes
{
    public int Id { get; set; }
    public string? Decisao { get; set; }
    public string? Comentario { get; set; }
    public DateTime DataHora { get; set; }
    public int UsuarioId { get; set; }
    public int PessoaId { get; set; }
    public int EmpresaId { get; set; }
}