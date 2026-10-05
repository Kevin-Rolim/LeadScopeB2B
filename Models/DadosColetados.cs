public class DadosColetados
{
    public int Id { get; set; }
    public string Campo { get; set; } = string.Empty;
    public int NivelConfianca { get; set; }
    public int FonteDadosID { get; set; }
    public FonteDados? FonteDados { get; set; }
    public int PessoaId { get; set; }
    public Pessoas? Pessoas { get; set; }
    public int EmpresaId { get; set; }
    public Empresas? Empresa { get; set; }
}