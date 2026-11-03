using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Integration.Universal;

namespace LeadScopeB2B.ViewModels.Integracoes;

public sealed class NovaApiForm
{
    public ContratoApi Contrato { get; set; } = new();
    public OperacaoApi Operacao { get; set; } = new();
    public AutenticacaoApi Autenticacao { get; set; } = new();
    public int FonteDadosId { get; set; }
}
public sealed class OperacaoApiForm
{
    public Guid Id { get; set; }
    public int Revisao { get; set; }
    public bool Nova { get; set; }
    public OperacaoApi Operacao { get; set; } = new();
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CorpoJson { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string SchemaJson { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string AmostraJson { get; set; } = "";
    public List<string> CaminhosDisponiveis { get; set; } = [];
    public List<Dictionary<string, string>>? Previa { get; set; }
    public List<string> Avisos { get; set; } = [];
    public IntegracaoApi Integracao { get; set; } = new();
}
public sealed class ConfiguracaoApiForm
{
    public Guid Id { get; set; }
    public int Revisao { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Nome { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UrlBase { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string PerfilRede { get; set; } = "publica";
    public bool Habilitada { get; set; }
    public int FonteDadosId { get; set; }
    public PlanoApi Plano { get; set; } = new();
    public IntegracaoApi Integracao { get; set; } = new();
    public ConsumoApi? Consumo { get; set; }
    public bool Automacao { get; set; }
    public Dictionary<string, string> CredenciaisConfiguradas { get; set; } = [];
}
public sealed class ExecutarApiForm
{
    public Guid Id { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string OperacaoId { get; set; } = "";
    public Dictionary<string, string> Parametros { get; set; } = [];
    public bool AutorizarEscrita { get; set; }
    public bool AutorizarCusto { get; set; }
    public bool AutorizarCustoDesconhecido { get; set; }
    public bool PersistirEvidencias { get; set; }
    public int? EmpresaId { get; set; }
    public int? PessoaId { get; set; }
    public IntegracaoApi Integracao { get; set; } = new();
    public OperacaoApi Operacao { get; set; } = new();
    public ResultadoApi? Resultado { get; set; }
}

public sealed class ConexaoApiForm
{
    public Guid Id { get; set; }
    public int Revisao { get; set; }
    public bool Habilitada { get; set; } = true;
    public bool Automacao { get; set; }
    public bool AutorizarConsumoCobrado { get; set; }
    public List<string> Operacoes { get; set; } = [];
    public Dictionary<string, string> ParametrosAutomacao { get; set; } = [];
}
