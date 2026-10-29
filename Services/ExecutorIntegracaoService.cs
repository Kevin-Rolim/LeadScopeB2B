using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Services;

public sealed partial class ExecutorIntegracaoService(IntegracaoArquivoRepository arquivos, IntegracaoRepository dados, DestinosHttp rede,
    IAutenticacaoApi auth, ApiHttpService http, IMapeadorApi mapper, ControleExecucaoApi controle,
    ConsumoIntegracaoService consumo, IOptions<MotorIntegracoesOptions> options, ILogger<ExecutorIntegracaoService> logger) : IExecutorApi
{
    public async Task<ResultadoApi> ExecutarAsync(ExecucaoApi execucao, CancellationToken ct)
    {
        await controle.Exclusao.WaitAsync(ct);
        var result = new ResultadoApi();
        try
        {
            if (!await dados.UsuarioPodeIntegrarAsync(execucao.UsuarioId, ct)) throw new IntegracaoException("SEM_PERMISSAO", "Somente administrador existente pode executar integrações.");
            var state = await arquivos.LerAsync(ct);
            var api = state.Integracoes.SingleOrDefault(a => a.Id == execucao.IntegracaoId) ?? throw new IntegracaoException("INTEGRACAO", "Integração não encontrada.");
            ValidacaoContrato.Validar(api.Contrato, rede);
            var op = api.Contrato.Operacoes.SingleOrDefault(o => o.Id == execucao.OperacaoId) ?? throw new IntegracaoException("OPERACAO", "Operação não encontrada.");
            if (!api.Habilitada || !op.Habilitada || !op.Revisada || !api.Contrato.Plano.UsoAutorizado) throw new IntegracaoException("DESABILITADA", "Integração/operação desabilitada ou uso ainda não autorizado.");
            if (ValidacaoContrato.Escrita(op) && (!op.PermitirEscrita || !execucao.AutorizarEscrita || execucao.Automatica && (!op.Rotina.PermitirEscrita || !options.Value.PermitirEscritaAutomatica)))
                throw new IntegracaoException("ESCRITA_NAO_AUTORIZADA", "Operação modifica dados externos e exige autorização específica; automação de escrita permanece bloqueada por padrão.");
            DestinoDadosApi? destino = null;
            if (execucao.EmpresaId.HasValue || execucao.PessoaId.HasValue || execucao.PersistirEvidencias)
            {
                destino = await dados.ObterDestinoIntegracaoAsync(execucao.EmpresaId, execucao.PessoaId, ct) ?? throw new IntegracaoException("DESTINO", "Selecione exatamente uma empresa ou pessoa existente e disponível.");
                foreach (var p in op.Parametros.Where(p => p.CampoOrigem != ""))
                    if (destino.Campos.TryGetValue(p.CampoOrigem, out var value) && value is not null) execucao.Parametros[p.Nome] = value;
            }
            if (execucao.PersistirEvidencias)
            {
                if (api.FonteDadosId <= 0 || !await dados.FonteIntegracaoAtivaAsync(api.FonteDadosId, ct)) throw new IntegracaoException("FONTE", "Configure uma fonte ativa para persistir evidências.");
                if (op.Mapeamentos.Count == 0) throw new IntegracaoException("SEM_DADOS_UTEIS", "A operação não possui mapeamento de evidências. Configure os campos antes de enriquecer.");
                if (op.Mapeamentos.Any(m => !m.Destino.StartsWith(destino!.PessoaId.HasValue ? "Pessoa." : "Empresa.", StringComparison.Ordinal)))
                    throw new IntegracaoException("DESTINO", "Vínculos/CNAEs e múltiplas entidades são suportados em prévia; este esquema só permite evidências de uma empresa ou pessoa existente por execução.");
                if (op.Paginacao.Tipo != "none") throw new IntegracaoException("DESTINO", "Persistência de evidências exige resposta única, sem paginação, para impedir associações ambíguas.");
            }
            var parameters = PrepararParametros(op, execucao.Parametros);
            if (op.ConsultaCota is null) await AtualizarCotaInternaAsync(api, execucao.UsuarioId, ct);
            var page = op.Paginacao.Inicio; var cursor = "";
            var visited = new HashSet<string>();
            var bodies = new HashSet<string>();
            var maxPages = op.Paginacao.Tipo == "none" ? 1 : op.Paginacao.MaxPaginas;
            for (var n = 0; n < maxPages; n++)
            {
                ct.ThrowIfCancellationRequested();
                RespostaHttpApi? response = null;
                for (var attempt = 0; attempt <= op.TentativasAdicionais; attempt++)
                {
                    using var request = Construir(api, op, parameters, page, cursor);
                    await auth.AplicarAsync(api, op, execucao, request, ct);
                    response = await http.EnviarAsync(api, op, execucao, request, ct);
                    result.HttpStatus = response.Status;
                    if (response.Status is not (502 or 503 or 504) || ValidacaoContrato.Escrita(op) || attempt == op.TentativasAdicionais || response.RetryAfter.HasValue) break;
                    await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct);
                }
                if (response!.Status == 401 && op.AutenticacaoId != "")
                {
                    var configAuth = api.Contrato.Autenticacoes.Single(a => a.Id == op.AutenticacaoId);
                    if (configAuth.Tipo is "clientCredentials" or "authorizationCode")
                    {
                        var invalid = await arquivos.LerSegredoAsync(api.Id, configAuth.Id, ct);
                        invalid.AccessToken = ""; invalid.ExpiraEm = DateTimeOffset.MinValue;
                        await arquivos.SalvarSegredoAsync(api.Id, configAuth.Id, invalid, ct);
                    }
                }
                if (response.Status is < 200 or >= 300) throw new IntegracaoException(response.Status is not (401 or 403 or 429) && api.Contrato.Erros.TryGetValue(response.Status.ToString(CultureInfo.InvariantCulture), out var declared) ? "PROVEDOR_" + declared : response.Codigo, "Consulta recusada: HTTP " + response.Status + ". Consulte credenciais, permissões e limites; o corpo de erro não é armazenado.");
                if (op.Metodo == "HEAD" || response.Status == 204) break;
                using var json = JsonDocument.Parse(response.Corpo, new JsonDocumentOptions { MaxDepth = 32 });
                var root = json.RootElement;
                if (op.SchemaResposta is { } schema) JsonSeguro.ValidarResposta(root, schema);
                if (op.Correspondencia is { } match)
                {
                    var actual = JsonSeguro.Selecionar(root, match.Caminho);
                    if (actual.ValueKind != JsonValueKind.String || !parameters.TryGetValue(match.Parametro, out var expected) || MapeadorApi.Transformar(actual.GetString()!, match.Normalizacao) != MapeadorApi.Transformar(expected, match.Normalizacao))
                        throw new IntegracaoException("IDENTIDADE", "Identidade da resposta difere do parâmetro consultado.");
                }
                if (op.ConsultaCota is not null) await consumo.RegistrarCotaAsync(api.Id, op, root, ct);
                var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(response.Corpo));
                if (!bodies.Add(fingerprint)) { result.Avisos.Add("Paginação interrompida: o provedor repetiu a mesma página."); break; }
                var items = mapper.Mapear(root, op, result.Avisos);
                if (result.Itens.Count + items.Count > 1000) throw new IntegracaoException("LIMITE_ITENS", "Execução limitada a 1.000 itens.");
                await FiltrarSegredosAsync(api, op, items, result.Avisos, ct);
                result.Itens.AddRange(items);
                if (n == 0) result.AmostraJson = await AmostraSeguraAsync(root, api, op, ct);
                if (op.Paginacao.Tipo == "none" || items.Count == 0) break;
                if (op.Paginacao.Tipo == "cursor")
                {
                    var next = JsonSeguro.Selecionar(root, op.Paginacao.CaminhoProximoCursor);
                    if (next.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) break;
                    if (next.ValueKind != JsonValueKind.String || next.GetString()!.Length > 2000) throw new IntegracaoException("CURSOR", "Cursor de paginação inválido.");
                    cursor = next.GetString()!;
                    if (cursor == "") break;
                    if (!visited.Add(cursor)) { result.Avisos.Add("Paginação interrompida: cursor repetido."); break; }
                }
                else
                {
                    if (items.Count < op.Paginacao.Tamanho) break;
                    page += op.Paginacao.Tipo == "offset" ? op.Paginacao.Tamanho : 1;
                }
                if (n == maxPages - 1) result.Avisos.Add("Limite de páginas atingido; execução encerrada sem agendar continuação implícita.");
            }
            if (execucao.PersistirEvidencias)
            {
                if (result.Itens.Count != 1) throw new IntegracaoException("DESTINO_AMBIGUO", "Persistência exige exatamente um item para o destino selecionado.");
                result.CamposExtraidos = result.Itens[0].Count;
                if (!result.Itens[0].Keys.Any(k=>k != "Empresa.Cnpj"))
                    throw new IntegracaoException("SEM_DADOS_UTEIS", "A resposta não contém campos úteis para revisão. Nenhuma evidência foi gravada; confira o mapeamento e a resposta do provedor.");
                try { result.Evidencias = await dados.SalvarEvidenciasIntegracaoAsync(destino!, result.Itens[0], api.FonteDadosId, execucao.UsuarioId, ct); }
                catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or IOException)
                {
                    logger.LogWarning("Persistência de integração {IntegracaoId}/{OperacaoId}, empresa {EmpresaId}, pessoa {PessoaId}: falha {Tipo}.", api.Id,op.Id,execucao.EmpresaId,execucao.PessoaId,ex.GetType().Name);
                    throw new IntegracaoException("PERSISTENCIA", "A consulta respondeu, mas as evidências não puderam ser gravadas. Nenhum enriquecimento foi concluído.");
                }
                result.Duplicidades = result.CamposExtraidos - result.Evidencias;
            }
            await arquivos.AlterarAsync(s => { var current = s.Integracoes.SingleOrDefault(a=>a.Id==api.Id); if (current is not null) { current.EstadoConexao="Conectada"; current.ErroConexao=null; current.ConectadaEm=DateTimeOffset.UtcNow; } return true; }, ct);
            result.CamposExtraidos = result.Itens.Sum(i=>i.Count);
            result.Sucesso = true;
            result.Codigo = execucao.PersistirEvidencias && result.Evidencias==0 ? "SEM_NOVIDADES" : "CONCLUIDA";
            result.Mensagem = execucao.PersistirEvidencias
                ? result.Evidencias>0 ? $"Consulta concluída: {result.Evidencias} evidências gravadas para revisão humana; {result.Duplicidades} duplicidades evitadas. O cadastro permanece inalterado até aprovação."
                    : "Nenhuma informação nova encontrada. As evidências desta resposta já existem; confira as revisões."
                : "Consulta concluída em prévia. Nenhuma evidência foi gravada e nenhum cadastro foi enriquecido.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { result.Codigo = "CANCELADA"; result.Mensagem = "Execução cancelada."; throw; }
        catch (IntegracaoException ex) { result.Codigo = ex.Codigo; result.Mensagem = ex.Message; }
        catch (ConflitoException ex) { result.Codigo = "PERSISTENCIA_CONFLITO"; result.Mensagem = ex.Message; }
        catch (ValidationException) { result.Codigo = "VALIDACAO"; result.Mensagem = "Dados incompatíveis com a validação do destino."; }
        catch (JsonException) { result.Codigo = "JSON_INVALIDO"; result.Mensagem = "Resposta ou parâmetro JSON inválido."; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { logger.LogWarning("Execução de integração {IntegracaoId}/{OperacaoId}: falha {Tipo}.", execucao.IntegracaoId,execucao.OperacaoId,ex.GetType().Name); result.Codigo = "FALHA"; result.Mensagem = "Execução não concluída; verifique a disponibilidade do destino e do armazenamento."; }
        finally
        {
            try
            {
                await arquivos.AlterarAsync(state =>
                {
                    state.Historico.Add(new() { Id = result.ExecucaoId, IntegracaoId = execucao.IntegracaoId, OperacaoId = execucao.OperacaoId, UsuarioId = execucao.UsuarioId, Codigo = result.Codigo, Status = result.HttpStatus, EmpresaId=execucao.EmpresaId, PessoaId=execucao.PessoaId, CamposExtraidos=result.CamposExtraidos, Evidencias=result.Evidencias, Duplicidades=result.Duplicidades, Avisos=result.Avisos.Take(30).ToList() });
                    if (state.Historico.Count > 2000) state.Historico.RemoveRange(0, state.Historico.Count - 2000);
                    return true;
                }, CancellationToken.None);
            }
            finally {
                logger.LogInformation("Integração {IntegracaoId}/{OperacaoId}, empresa {EmpresaId}, pessoa {PessoaId}: {Codigo}, HTTP {HttpStatus}, campos {Campos}, evidências {Evidencias}, duplicidades {Duplicidades}.",execucao.IntegracaoId,execucao.OperacaoId,execucao.EmpresaId,execucao.PessoaId,result.Codigo,result.HttpStatus,result.CamposExtraidos,result.Evidencias,result.Duplicidades);
                controle.Exclusao.Release();
            }
        }
        return result;
    }
    private static Dictionary<string, string> PrepararParametros(OperacaoApi op, Dictionary<string, string> input)
    {
        if (input.Keys.Any(k => !op.Parametros.Any(p => p.Nome == k))) throw new IntegracaoException("PARAMETRO", "Parâmetro não declarado no contrato.");
        var output = new Dictionary<string, string>();
        foreach (var p in op.Parametros)
        {
            var value = input.GetValueOrDefault(p.Nome) ?? p.Padrao;
            if (string.IsNullOrEmpty(value) && !p.Obrigatorio) continue;
            if (value is null) throw new IntegracaoException("PARAMETRO", $"Parâmetro obrigatório ausente: {p.Nome}.");
            output[p.Nome] = ValidacaoContrato.ValidarParametro(p, value);
        }
        return output;
    }
    private static HttpRequestMessage Construir(IntegracaoApi api, OperacaoApi op, Dictionary<string, string> input, int page, string cursor)
    {
        var path = op.Caminho;
        foreach (var p in op.Parametros.Where(p => p.Local == "path"))
        {
            if (!input.TryGetValue(p.Nome, out var value)) throw new IntegracaoException("PARAMETRO", "Parâmetro de rota ausente.");
            if (value is "." or "..") throw new IntegracaoException("PARAMETRO", "Segmento de rota inválido.");
            path = path.Replace("{" + p.Nome + "}", Uri.EscapeDataString(value));
        }
        var query = op.Parametros.Where(p => p.Local == "query" && input.ContainsKey(p.Nome)).ToDictionary(p => p.Nome, p => input[p.Nome]);
        if (op.Paginacao.Tipo != "none")
        {
            if (op.Paginacao.Tipo != "cursor" || cursor != "") query[op.Paginacao.Parametro] = op.Paginacao.Tipo == "cursor" ? cursor : page.ToString(CultureInfo.InvariantCulture);
            if (op.Paginacao.ParametroTamanho != "") query[op.Paginacao.ParametroTamanho] = op.Paginacao.Tamanho.ToString(CultureInfo.InvariantCulture);
        }
        var url = api.Contrato.UrlBase.TrimEnd('/') + path + (query.Count == 0 ? "" : "?" + string.Join('&', query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))));
        var request = new HttpRequestMessage(new HttpMethod(op.Metodo), url) { Version = System.Net.HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
        request.Headers.Accept.ParseAdd("application/json");
        foreach (var p in op.Parametros.Where(p => p.Local == "header" && input.ContainsKey(p.Nome))) request.Headers.Add(p.Nome, input[p.Nome]);
        var body = op.Corpo.HasValue ? JsonNode.Parse(op.Corpo.Value.GetRawText()) : null;
        foreach (var p in op.Parametros.Where(p => p.Local == "body" && input.ContainsKey(p.Nome)))
        {
            body ??= new JsonObject(); var text = input[p.Nome];
            ((JsonObject)body)[p.Nome] = p.Tipo == "string" ? JsonValue.Create(text) : JsonNode.Parse(text);
        }
        if (body is not null || op.Corpo.HasValue) request.Content = new StringContent(body?.ToJsonString() ?? "null", Encoding.UTF8, "application/json");
        return request;
    }
    private async Task FiltrarSegredosAsync(IntegracaoApi api, OperacaoApi op, List<Dictionary<string, string>> itens, List<string> avisos, CancellationToken ct)
    {
        var secrets = await ValoresSegredosAsync(api, ct);
        foreach (var item in itens) foreach (var key in item.Keys.ToArray())
        {
            if (!secrets.Any(secret => item[key].Contains(secret, StringComparison.Ordinal))) continue;
            if (op.Mapeamentos.Any(m => m.Destino == key && m.Obrigatorio)) throw new IntegracaoException("SEGREDO_NA_RESPOSTA", "O provedor devolveu uma credencial em um campo obrigatório; resultado descartado.");
            item.Remove(key); avisos.Add("Campo descartado porque contém credencial da integração.");
        }
    }
    private async Task<List<string>> ValoresSegredosAsync(IntegracaoApi api, CancellationToken ct)
    {
        var values = new List<string>();
        foreach (var a in api.Contrato.Autenticacoes)
        {
            var s = await arquivos.LerSegredoAsync(api.Id, a.Id, ct);
            var raw = new[] { s.Valor, s.Usuario, s.ClienteId, s.ClienteSegredo, s.AccessToken, s.RefreshToken, s.VerificadorPkce }.Where(x => x.Length > 0).ToArray();
            values.AddRange(raw); values.AddRange(raw.Select(Uri.EscapeDataString));
            if (s.Usuario != "" && s.Valor != "") values.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(s.Usuario + ":" + s.Valor)));
            if (s.ClienteId != "" && s.ClienteSegredo != "") values.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.EscapeDataString(s.ClienteId) + ":" + Uri.EscapeDataString(s.ClienteSegredo))));
        }
        return values;
    }
    private async Task<string> AmostraSeguraAsync(JsonElement root, IntegracaoApi api, OperacaoApi op, CancellationToken ct)
    {
        var secretValues = await ValoresSegredosAsync(api, ct);
        object? Convert(JsonElement element, int depth = 0)
        {
            if (depth > 8) return "[profundidade omitida]";
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().Take(40).Select((p, i) => new { Nome = secretValues.Any(s => p.Name.Contains(s, StringComparison.Ordinal)) ? $"[chave omitida {i}]" : p.Name, Valor = ValidacaoContrato.NomeSensivel(p.Name) || op.CamposOmitidosAmostra.Contains(p.Name, StringComparer.OrdinalIgnoreCase) || p.Name.Equals("CPF", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("CPFCNPJ", StringComparison.OrdinalIgnoreCase) ? (object?)"[segredo omitido]" : Convert(p.Value, depth + 1) }).ToDictionary(p => p.Nome, p => p.Valor),
                JsonValueKind.Array => element.EnumerateArray().Take(5).Select(x => Convert(x, depth + 1)).ToArray(),
                JsonValueKind.String => secretValues.Any(s => element.GetString()!.Contains(s, StringComparison.Ordinal)) ? "[segredo omitido]" : element.GetString()![..Math.Min(500, element.GetString()!.Length)],
                JsonValueKind.Number => element.GetRawText(), JsonValueKind.True => true, JsonValueKind.False => false, _ => null
            };
        }
        var text = JsonSerializer.Serialize(Convert(root), new JsonSerializerOptions { WriteIndented = true });
        return text.Length <= 16384 ? text : text[..16384] + "\n[amostra truncada]";
    }
}
