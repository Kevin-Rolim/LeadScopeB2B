public class Fontes_dados
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Url { get; set; }
    public decimal? Confiabilidade { get; set; }
    public DateTime DataHoraMod { get; set; }
    public DateTime? DataHoraDel { get; set; }
}