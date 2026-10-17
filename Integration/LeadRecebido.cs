using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.Integration;

public class LeadRecebido
{
    [Range(1, int.MaxValue)] public int PessoaId { get; set; }
    [Range(1, int.MaxValue)] public int EmpresaId { get; set; }
    [StringLength(100)] public string? Cargo { get; set; }
    [StringLength(100)] public string? Departamento { get; set; }
    [StringLength(50)] public string? Senioridade { get; set; }
    [Range(0, 100)] public decimal? NivelConfianca { get; set; }
}
