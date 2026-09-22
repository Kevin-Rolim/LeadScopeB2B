public class Dados_coletados
{
    public int Id { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public decimal? NivelConfianca { get; set; }
    public int FonteDadosId { get; set; }
    public int? PessoaId { get; set; }
    public int? EmpresaId { get; set; }
}