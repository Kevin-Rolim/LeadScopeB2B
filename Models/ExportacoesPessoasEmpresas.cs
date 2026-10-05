public class ExportacoesPessoasEmpresas
{
    public int ExportacoesId { get; set; }
    public Exportacoes? Exportacoes { get; set; }
    public int PessoaId { get; set; }
    public int EmpresaId { get; set; }
    public PessoasEmpresas? PessoasEmpresas { get; set; }
}