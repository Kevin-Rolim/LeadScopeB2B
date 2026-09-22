public class Empresas
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? Cnpj { get; set; }
    public string? Site { get; set; }
    public string? Segmento { get; set; }
    public string? Porte { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Status { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}