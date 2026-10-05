public class PessoasEmpresas
{
    public int PessoaId { get; set; }
    public Pessoas? Pessoas { get; set; }
    public int EmpresaId { get; set; }
    public Empresas? Empresas { get; set; }
    public string Cargo { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Senioridade { get; set; } = string.Empty;
    public string StatusVinculo { get; set; } = string.Empty;
    public string StatusLead { get; set; } = string.Empty;
    public DateTime DataRevisao { get; set; }
    public int NivelConfianca { get; set; }
    public string FonteVinculo { get; set; } = string.Empty;
    public DateTime DataVerif { get; set; }
    public List<Revisoes>? Revisoes { get; set; }
    public List<ExportacoesPessoasEmpresas>? ExportacoesPessoasEmpresas { get; set; }
}