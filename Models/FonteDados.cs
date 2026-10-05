public class FonteDados
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Tipo { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Confiabilidade { get; set; }
    public DateTime DataHoraMod { get; set; }
    public DateTime DataHoraDel { get; set; }
}
