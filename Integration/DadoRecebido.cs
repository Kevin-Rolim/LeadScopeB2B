using System.ComponentModel.DataAnnotations;
namespace LeadScopeB2B.Integration;

public class DadoRecebido : IValidatableObject
{
    [Range(1, int.MaxValue)] public int? PessoaId { get; set; }
    [Range(1, int.MaxValue)] public int? EmpresaId { get; set; }
    [Required, StringLength(100)] public string Campo { get; set; } = "";
    [Required, StringLength(2000)] public string Valor { get; set; } = "";
    [Range(0, 100)] public decimal? NivelConfianca { get; set; }
    [StringLength(1000), Url] public string? EvidenciaUrl { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PessoaId.HasValue == EmpresaId.HasValue) yield return new ValidationResult("Informe exatamente um destino: pessoaId ou empresaId.");
    }
}
