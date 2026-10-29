using LeadScopeB2B.Integration.Universal;
namespace LeadScopeB2B.ViewModels.Empresas;

public sealed class EnriquecimentoEmpresaViewModel
{
    public string Estado { get; set; } = "Ainda não consultada";
    public List<OpcaoEnriquecimento> Operacoes { get; set; } = [];
    public List<EvidenciaEmpresa> Evidencias { get; set; } = [];
    public HistoricoApi? UltimaExecucao { get; set; }
}
public sealed record OpcaoEnriquecimento(Guid IntegracaoId, string OperacaoId, string Nome, bool CustoDesconhecido, bool Cobrada, string? Bloqueio);
public sealed record EvidenciaEmpresa(int Id, string Campo, string Valor, string Fonte, string Estado);
