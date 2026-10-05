using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class CnaesEmpresas
{
    [Required]
    [StringLength(20)]
    public string CnaeNumero { get; set; } = string.Empty;

    public Cnaes Cnaes { get; set; } = null!;

    [Required]
    public int EmpresaId { get; set; }

    public Empresas Empresas { get; set; } = null!;
}
