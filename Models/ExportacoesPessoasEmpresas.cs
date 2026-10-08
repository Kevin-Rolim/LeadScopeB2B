using System.ComponentModel.DataAnnotations;

namespace LeadScopeB2B.Models;

public class ExportacoesPessoasEmpresas
{
    [Required]
    public int ExportacoesId { get; set; }

    public Exportacoes Exportacoes { get; set; } = null!;

    [Required]
    public int PessoaId { get; set; }

    [Required]
    public int EmpresaId { get; set; }

    public PessoasEmpresas PessoasEmpresas { get; set; } = null!;
}
