public class Revisoes
{
    public int Id { get; set; }
    public string Decisao { get; set; } = string.Empty;
    public string Comentario { get; set; } = string.Empty;
    public DateTime DataHora { get; set; }
    public int UsuarioId { get; set; }
    public Usuarios? Usuarios { get; set; }
    public int PessoaId { get; set; }
    public int EmpresaId { get; set; }
    public PessoasEmpresas? PessoasEmpresas { get; set; }
}