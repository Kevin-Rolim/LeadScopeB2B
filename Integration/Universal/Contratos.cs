using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LeadScopeB2B.Integration.Universal;

// Contrato público, declarativo. Nenhum tipo CLR ou script é carregado a partir dele.
public sealed class ContratoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string VersaoContrato { get; set; } = "1.0";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Provedor { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Categoria { get; set; } = "REST/JSON";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Documentacao { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UrlBase { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string PerfilRede { get; set; } = "publica";
    public List<AutenticacaoApi> Autenticacoes { get; set; } = [];
    public List<OperacaoApi> Operacoes { get; set; } = [];
    public CapacidadesApi Capacidades { get; set; } = new();
    public PlanoApi Plano { get; set; } = new();
    public Dictionary<string, string> Erros { get; set; } = [];
}
public sealed class AutenticacaoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Id { get; set; } = "principal";
    // none, apiKeyHeader, apiKeyQuery, bearer, basic, clientCredentials, authorizationCode
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Tipo { get; set; } = "none";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Nome { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string TokenUrl { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string AutorizacaoUrl { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Escopos { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string EnvioCliente { get; set; } = "basic";
}
public sealed class OperacaoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Id { get; set; } = "consulta";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Nome { get; set; } = "Consulta";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Metodo { get; set; } = "GET";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Caminho { get; set; } = "/";
    public bool Habilitada { get; set; }
    public bool Revisada { get; set; }
    public bool PermitirEscrita { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string AutenticacaoId { get; set; } = "";
    public string? Incompatibilidade { get; set; }
    public List<ParametroApi> Parametros { get; set; } = [];
    public JsonElement? Corpo { get; set; }
    public JsonElement? SchemaResposta { get; set; }
    public List<MapeamentoApi> Mapeamentos { get; set; } = [];
    public List<string> CamposOmitidosAmostra { get; set; } = [];
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoItens { get; set; } = "$";
    public CorrespondenciaApi? Correspondencia { get; set; }
    public PaginacaoApi Paginacao { get; set; } = new();
    public int TimeoutSegundos { get; set; } = 30;
    public int TentativasAdicionais { get; set; }
    public int MaxItensPorPagina { get; set; } = 100;
    public CustoApi Custo { get; set; } = new();
    public RotinaApi Rotina { get; set; } = new();
    public CotaApi? ConsultaCota { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CabecalhoLimiteRpm { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CabecalhoRestante { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UnidadeCotaCabecalho { get; set; } = "requisicao";
    public List<string> Permissoes { get; set; } = [];
}
public sealed class ParametroApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Nome { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Local { get; set; } = "query"; // path, query, header, body
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Tipo { get; set; } = "string";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Formato { get; set; } = ""; // cnpj, email, uri
    public bool Obrigatorio { get; set; }
    public int MaxComprimento { get; set; } = 2000;
    public decimal? Minimo { get; set; }
    public decimal? Maximo { get; set; }
    public List<string> Valores { get; set; } = [];
    public string? Padrao { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CampoOrigem { get; set; } = ""; // Empresa.Cnpj, Pessoa.Email...
}
public sealed class MapeamentoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Caminho { get; set; } = "$.name";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Destino { get; set; } = "Empresa.RazaoSocial";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Tipo { get; set; } = "string";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Transformacao { get; set; } = "trim";
    public bool Obrigatorio { get; set; }
}
public sealed class CorrespondenciaApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Parametro { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Caminho { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Normalizacao { get; set; } = "trim";
}
public sealed class PaginacaoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Tipo { get; set; } = "none"; // page, offset, cursor
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Parametro { get; set; } = "page";
    public int Inicio { get; set; } = 1;
    public int Tamanho { get; set; } = 20;
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string ParametroTamanho { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoProximoCursor { get; set; } = "";
    public int MaxPaginas { get; set; } = 1;
}
public sealed class RotinaApi
{
    public bool Habilitada { get; set; }
    public int UsuarioId { get; set; }
    public int IntervaloMinutos { get; set; } = 720;
    public int LimitePorExecucao { get; set; } = 20;
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Criterio { get; set; } = "parametrosFixos"; // empresasSemEvidencias, pessoasSemEvidencias
    public bool PersistirEvidencias { get; set; }
    public bool PermitirEscrita { get; set; }
    public bool AutorizarCusto { get; set; }
    public bool AutorizarCustoDesconhecido { get; set; }
    public Dictionary<string, string> Parametros { get; set; } = [];
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string PoliticaErros { get; set; } = "continuar"; // parar
}
public sealed class LimitesApi
{
    public int? PorSegundo { get; set; }
    public int? PorMinuto { get; set; }
    public int? Diario { get; set; }
    public int? Mensal { get; set; }
    public decimal? CreditosMensais { get; set; }
}
public sealed class PlanoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Nome { get; set; } = "Não informado";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CondicoesUso { get; set; } = "";
    public bool UsoAutorizado { get; set; }
    public LimitesApi Administrador { get; set; } = new();
    public LimitesApi Sistema { get; set; } = new() { PorMinuto = 6, Diario = 20, Mensal = 200 };
    public decimal? OrcamentoMensal { get; set; }
    public decimal? Mensalidade { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Moeda { get; set; } = "BRL";
}
public sealed class CustoApi
{
    // null significa desconhecido; nunca é convertido implicitamente em zero.
    public decimal? PorRequisicao { get; set; }
    public decimal? PorItem { get; set; }
    public decimal? ReservaCustoDesconhecido { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Unidade { get; set; } = "requisicao";
    public decimal CreditosPorUnidade { get; set; } = 1;
}
public sealed class CotaApi
{
    public int AtualizarMinutos { get; set; } = 30;
    public bool NaoConsomeCota { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoCompetencia { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoUsado { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoLimite { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoRenovacao { get; set; } = "";
    public bool LimiteZeroSemLimite { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string FormatoRenovacao { get; set; } = "data";

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UnidadeRestante { get; set; } = "requisicao";
    public bool RestanteNuloSemLimite { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoRpm { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CaminhoRestante { get; set; } = "";
}
public sealed class IntegracaoApi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Revisao { get; set; } = 1;
    public string CatalogoId { get; set; } = "";
    public bool SelecaoConcluida { get; set; }
    public List<string> OperacoesSelecionadas { get; set; } = [];
    public bool AutomacaoPelaInterface { get; set; }
    public string EstadoConexao { get; set; } = "Não conectada";
    public string? ErroConexao { get; set; }
    public DateTimeOffset? ConectadaEm { get; set; }
    public bool Habilitada { get; set; }
    public int FonteDadosId { get; set; }
    public ContratoApi Contrato { get; set; } = new();
    public DateTimeOffset ModificadaEm { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class SegredoApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Valor { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Usuario { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string ClienteId { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string ClienteSegredo { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string AccessToken { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiraEm { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string EstadoOAuth { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string VerificadorPkce { get; set; } = "";
    public int UsuarioOAuth { get; set; }
    public bool OAuthAutorizaCusto { get; set; }
    public bool OAuthAutorizaDesconhecido { get; set; }
    public DateTimeOffset EstadoExpiraEm { get; set; }
}
public sealed class MotorIntegracoesOptions
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Diretorio { get; set; } = "App_Data/Integracoes";
    public bool AutomacaoHabilitada { get; set; }
    public bool PermitirEscritaAutomatica { get; set; }
    public int IntervaloVerificacaoSegundos { get; set; } = 30;
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UrlPublicaOAuth { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string RedesPrivadasJson { get; set; } = "";
    public List<PerfilRedeApi> RedesPrivadas { get; set; } = [];
}
public sealed class PerfilRedeApi
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Id { get; set; } = "";
    public List<string> Origens { get; set; } = [];
    public List<string> Redes { get; set; } = [];
    public bool PermitirHttp { get; set; }
}
public sealed class ExecucaoApi
{
    public Guid IntegracaoId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string OperacaoId { get; set; } = "";
    public Dictionary<string, string> Parametros { get; set; } = [];
    public int UsuarioId { get; set; }
    public bool AutorizarEscrita { get; set; }
    public bool AutorizarCusto { get; set; }
    public bool AutorizarCustoDesconhecido { get; set; }
    public bool Automatica { get; set; }
    public bool PersistirEvidencias { get; set; }
    public int? EmpresaId { get; set; }
    public int? PessoaId { get; set; }
}
public sealed class ResultadoApi
{
    public Guid ExecucaoId { get; set; } = Guid.NewGuid();
    public bool Sucesso { get; set; }
    public int? HttpStatus { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Codigo { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Mensagem { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string AmostraJson { get; set; } = "";
    public List<Dictionary<string, string>> Itens { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
    public int Evidencias { get; set; }
    public int CamposExtraidos { get; set; }
    public int Duplicidades { get; set; }
}
public sealed class HistoricoApi
{
    public List<string> Avisos { get; set; } = [];
    public int? EmpresaId { get; set; }
    public int? PessoaId { get; set; }
    public int CamposExtraidos { get; set; }
    public int Evidencias { get; set; }
    public int Duplicidades { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IntegracaoId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string OperacaoId { get; set; } = "";
    public int UsuarioId { get; set; }
    public DateTimeOffset Data { get; set; } = DateTimeOffset.UtcNow;
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Codigo { get; set; } = "RESERVADA";
    public int? Status { get; set; }
    public decimal Reserva { get; set; }
    public decimal Creditos { get; set; }
    public bool CustoDesconhecido { get; set; }
}
public sealed class ConsumoApi
{
    public Dictionary<string, int> RequisicoesDia { get; set; } = [];
    public Dictionary<string, int> ConsultasGerenciaisDia { get; set; } = [];
    public Dictionary<string, decimal> CustoMes { get; set; } = [];
    public Dictionary<string, decimal> CreditosMes { get; set; } = [];
    public DateTimeOffset ProximaRequisicao { get; set; }
    public DateTimeOffset BloqueadaAte { get; set; }
    public int? RestanteJanela { get; set; }
    public DateTimeOffset? JanelaAte { get; set; }
    public string UnidadeJanela { get; set; } = "requisicao";
    public DescobertaLimitesApi? Endpoint { get; set; }
    public DescobertaLimitesApi? Headers { get; set; }
    public DateTimeOffset? UltimaTentativaCota { get; set; }
    public string? ErroDescoberta { get; set; }
    public Dictionary<string, int> ConsumoCustoDesconhecido { get; set; } = [];
    public int? RpmProvedor { get; set; }
    public int? RestanteProvedor { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string UnidadeCotaProvedor { get; set; } = "requisicao";
    public DateTimeOffset? CotaObservadaEm { get; set; }
    public Dictionary<string, int> UltimoRegistroRotina { get; set; } = [];
    public Dictionary<string, DateTimeOffset> ProximaRotina { get; set; } = [];
}
public sealed class EstadoIntegracoes
{
    public int Versao { get; set; } = 1;
    public List<IntegracaoApi> Integracoes { get; set; } = [];
    public List<IntegracaoApi> ContratosAnteriores { get; set; } = [];
    public Dictionary<Guid, ConsumoApi> Consumo { get; set; } = [];
    public List<HistoricoApi> Historico { get; set; } = [];
}
public sealed record ImportacaoApi(ContratoApi Contrato, IReadOnlyList<string> Avisos);
public interface IImportadorApi { ImportacaoApi Importar(string documento); }
public interface IMapeadorApi { List<Dictionary<string, string>> Mapear(JsonElement root, OperacaoApi operacao, List<string> avisos); }
public interface IAutenticacaoApi { Task AplicarAsync(IntegracaoApi api, OperacaoApi operacao, ExecucaoApi execucao, HttpRequestMessage request, CancellationToken ct); }
public interface IExecutorApi
{
    Task<ResultadoApi> ExecutarAsync(ExecucaoApi execucao, CancellationToken ct);
    Task AtualizarConsumosPendentesAsync(CancellationToken ct);
    Task AtualizarConsumoAsync(Guid id, int usuarioId, CancellationToken ct);
}
public interface IConsumoApi { Task<Guid> ReservarAsync(IntegracaoApi api, OperacaoApi op, ExecucaoApi execucao, CancellationToken ct); }
public interface ICatalogoApis { IReadOnlyList<ContratoApi> Listar(); }
public sealed class IntegracaoException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}

public sealed class DestinoDadosApi
{
    public int? EmpresaId { get; set; }
    public int? PessoaId { get; set; }
    public Dictionary<string, string?> Campos { get; set; } = [];
}
public sealed class ControleExecucaoApi
{
    public SemaphoreSlim Exclusao { get; } = new(1, 1);
}

// Capacidades declaradas: não se presumem endpoints, preços ou janelas não informados.
public sealed class CapacidadesApi
{
    public string OperacaoCota { get; set; } = "";
    public LimitesApi LimitesDeclarados { get; set; } = new();
    public string Origem { get; set; } = "contrato";
    public string Documentacao { get; set; } = "";
    public DateTimeOffset? VerificadoEm { get; set; }
    public bool GratuidadeDocumentada { get; set; }
}
public sealed class DescobertaLimitesApi
{
    public string Origem { get; set; } = "";
    public DateTimeOffset ObservadoEm { get; set; }
    public DateTimeOffset? RenovaEm { get; set; }
    public int? Rpm { get; set; }
    public int? Restante { get; set; }
    public int? Usado { get; set; }
    public int? Limite { get; set; }
    public string? Competencia { get; set; }
    public bool SemLimite { get; set; }
    public string Unidade { get; set; } = "requisicao";
    public double? IntervaloMinimoSegundos { get; set; }
}
