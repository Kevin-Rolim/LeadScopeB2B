using System.Text;
using System.Text.Json;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;

namespace LeadScopeB2B.Services;

public sealed class GerenciadorIntegracaoService(IntegracaoArquivoRepository arquivos, IImportadorApi importador, DestinosHttp rede, IntegracaoRepository dados, IExecutorApi executor, ICatalogoApis catalogo)
{
    public Task<EstadoIntegracoes> ListarAsync(CancellationToken ct) => arquivos.LerAsync(ct);
    public async Task<IntegracaoApi> ObterAsync(Guid id, CancellationToken ct) => (await arquivos.LerAsync(ct)).Integracoes.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Integração não encontrada.");
    public ImportacaoApi Importar(string documento) => importador.Importar(documento);
    public async Task<Guid> CriarAsync(ContratoApi contrato, int fonteId, CancellationToken ct, string catalogoId = "")
    {
        ValidacaoContrato.Validar(contrato, rede);
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(contrato, IntegracaoArquivoRepository.Json)) > 256 * 1024)
            throw new IntegracaoException("CONTRATO_GRANDE", "Contrato interno limitado a 256 KiB.");
        foreach (var op in contrato.Operacoes) { op.Habilitada = false; op.Revisada = false; op.Rotina.Habilitada = false; }
        contrato.Plano.UsoAutorizado = false;
        var api = new IntegracaoApi { Contrato = contrato, FonteDadosId = fonteId, CatalogoId = catalogoId };
        return await arquivos.AlterarAsync(state =>
        {
            if (state.Integracoes.Count >= 20) throw new IntegracaoException("LIMITE_CONFIGURACOES", "Esta versão permite até 20 integrações por instância.");
            state.Integracoes.Add(api); return api.Id;
        }, ct);
    }
    public async Task<ResultadoApi?> ConectarAsync(Guid id, int revisao, string? authId, SegredoApi? segredo,
        IReadOnlyCollection<string> operacoes, bool habilitada, bool automacao, bool autorizarConsumoCobrado, int usuarioId, CancellationToken ct, IReadOnlyDictionary<string, string>? parametrosAutomacao = null)
    {
        if (!await dados.UsuarioPodeIntegrarAsync(usuarioId, ct)) throw new IntegracaoException("SEM_PERMISSAO", "Somente um administrador pode gerenciar conexões.");
        var api = await ObterAsync(id, ct);
        if (api.Revisao != revisao) throw new IntegracaoException("CONCORRENCIA", "A conexão mudou. Reabra a página antes de continuar.");
        // Atualiza somente conhecimentos declarativos reconhecidos; não altera preços, segredos ou rotinas.
        var conhecido = catalogo.Listar().FirstOrDefault(c => c.Provedor==api.Contrato.Provedor && c.UrlBase.TrimEnd('/')==api.Contrato.UrlBase.TrimEnd('/') &&
            api.Contrato.Operacoes.All(o=>c.Operacoes.Any(k=>k.Id==o.Id && k.Metodo==o.Metodo && k.Caminho==o.Caminho && k.AutenticacaoId==o.AutenticacaoId)) &&
            api.Contrato.Autenticacoes.All(a=>c.Autenticacoes.Any(k=>k.Id==a.Id && k.Tipo==a.Tipo && k.Nome==a.Nome)));
        if (conhecido is not null)
        {
            api.CatalogoId = conhecido.Provedor;
            var capacidades = JsonSerializer.SerializeToElement(conhecido.Capacidades).Deserialize<CapacidadesApi>()!;
            if (!api.Contrato.Operacoes.Any(o=>o.Id==capacidades.OperacaoCota)) capacidades.OperacaoCota="";
            api.Contrato.Capacidades=capacidades;
            foreach(var op in api.Contrato.Operacoes.Where(o=>o.ConsultaCota is not null))
                if(conhecido.Operacoes.FirstOrDefault(k=>k.Id==op.Id)?.ConsultaCota is {} spec) op.ConsultaCota=JsonSerializer.SerializeToElement(spec).Deserialize<CotaApi>();
        }
        if (authId is not null && segredo is not null && new[] { segredo.Valor, segredo.Usuario, segredo.ClienteId, segredo.ClienteSegredo }.Any(v=>v.Length>0)) await SalvarCredencialAsync(id, authId, segredo, ct);
        var quota = api.Contrato.Operacoes.FirstOrDefault(o => o.ConsultaCota is not null && (api.Contrato.Capacidades.OperacaoCota == "" || o.Id == api.Contrato.Capacidades.OperacaoCota));
        foreach (var op in api.Contrato.Operacoes)
        {
            var selecionada = operacoes.Contains(op.Id);
            if (parametrosAutomacao is not null)
                foreach(var p in op.Parametros.Where(p=>p.CampoOrigem==""))
                    if(parametrosAutomacao.TryGetValue(op.Id+":"+p.Nome,out var value))
                    {
                        if (string.IsNullOrWhiteSpace(value)) op.Rotina.Parametros.Remove(p.Nome);
                        else
                        {
                            try { op.Rotina.Parametros[p.Nome] = ValidacaoContrato.ValidarParametro(p,value); }
                            catch(JsonException) { throw new IntegracaoException("PARAMETRO_INVALIDO", $"Confira o valor de {p.Nome} para a automação."); }
                        }
                    }
            if (selecionada && (op.Incompatibilidade is not null || ValidacaoContrato.Escrita(op) && (!op.Revisada || !op.PermitirEscrita)))
                throw new IntegracaoException("OPERACAO", "Operações incompatíveis ou de escrita exigem revisão nas opções avançadas.");
            var gratuita = op.Custo.PorRequisicao == 0 && op.Custo.PorItem == 0;
            if (habilitada && automacao && selecionada && !gratuita && !autorizarConsumoCobrado)
                throw new IntegracaoException("AUTOMACAO_CUSTO", "Esta API pode gerar cobrança. Autorize esse consumo antes de ativar a automação.");
            op.Habilitada = habilitada && (selecionada || op == quota);
            if (op.Habilitada) op.Revisada = true;
            op.Rotina.Habilitada = habilitada && automacao && selecionada && op != quota;
            if (op.Rotina.Habilitada)
            {
                op.Rotina.UsuarioId = usuarioId;
                op.Rotina.AutorizarCusto = autorizarConsumoCobrado;
                op.Rotina.AutorizarCustoDesconhecido = autorizarConsumoCobrado;
                if (op.Parametros.Any(p => p.CampoOrigem.StartsWith("Empresa.", StringComparison.Ordinal))) op.Rotina.Criterio = "empresasSemEvidencias";
                else if (op.Parametros.Any(p => p.CampoOrigem.StartsWith("Pessoa.", StringComparison.Ordinal))) op.Rotina.Criterio = "pessoasSemEvidencias";
                // Parâmetros exigidos não são inventados. Valores fixos são editáveis na operação.
                if (op.Parametros.Any(p => p.Obrigatorio && p.CampoOrigem == "" && p.Padrao is null && !op.Rotina.Parametros.ContainsKey(p.Nome)))
                    throw new IntegracaoException("AUTOMACAO_PARAMETROS", "Informe os valores usados pela automação na configuração da operação.");
            }
        }
        api.SelecaoConcluida = true; api.OperacoesSelecionadas = api.Contrato.Operacoes.Where(o=>operacoes.Contains(o.Id)).Select(o=>o.Id).ToList();
        api.Habilitada = habilitada; api.AutomacaoPelaInterface = habilitada && automacao;
        api.Contrato.Plano.UsoAutorizado = habilitada;
        api.ErroConexao = null; api.EstadoConexao = habilitada ? "Pronta para consultar; conexão ainda não verificada" : "Desativada";
        await SalvarAsync(api, ct);
        var revisaoConectando = api.Revisao;
        ResultadoApi? resultado = null;
        if (habilitada && quota is not null && quota.Metodo == "GET" && quota.Custo.PorRequisicao == 0 && quota.Custo.PorItem == 0)
        {
            resultado = await executor.ExecutarAsync(new() { IntegracaoId=id, OperacaoId=quota.Id, UsuarioId=usuarioId }, ct);
            api = await ObterAsync(id, ct);
            if (api.Revisao != revisaoConectando) throw new IntegracaoException("CONCORRENCIA", "A conexão mudou durante a verificação. Reabra a página.");
            api.EstadoConexao = resultado.Sucesso ? "Conectada" : "Não foi possível conectar";
            api.ErroConexao = resultado.Sucesso ? null : resultado.Mensagem;
            if (!resultado.Sucesso) { api.Habilitada = false; api.AutomacaoPelaInterface = false; foreach(var op in api.Contrato.Operacoes) op.Rotina.Habilitada=false; }
        }
        api = resultado is null ? await ObterAsync(id, ct) : api;
        if (api.Habilitada && api.FonteDadosId == 0 && api.Contrato.Operacoes.Any(o=>o.Habilitada && o.Mapeamentos.Any(m=>m.Destino.StartsWith("Empresa.", StringComparison.Ordinal) || m.Destino.StartsWith("Pessoa.", StringComparison.Ordinal))))
            api.FonteDadosId = await dados.PrepararFonteIntegracaoAsync(api.Contrato.Provedor, api.Contrato.UrlBase, ct);
        await SalvarAsync(api, ct);
        return resultado;
    }
    public async Task SalvarAsync(IntegracaoApi api, CancellationToken ct)
    {
        ValidacaoContrato.Validar(api.Contrato, rede);
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(api.Contrato, IntegracaoArquivoRepository.Json)) > 256 * 1024)
            throw new IntegracaoException("CONTRATO_GRANDE", "Contrato interno limitado a 256 KiB.");
        await arquivos.AlterarAsync(state =>
        {
            var index = state.Integracoes.FindIndex(x => x.Id == api.Id);
            if (index < 0) throw new KeyNotFoundException("Integração não encontrada.");
            if (state.Integracoes[index].Revisao != api.Revisao) throw new IntegracaoException("CONCORRENCIA", "A configuração mudou. Reabra a página antes de salvar.");
            if (state.Integracoes[index].Contrato.Plano.Moeda != api.Contrato.Plano.Moeda && state.Consumo.TryGetValue(api.Id, out var uso) && uso.RequisicoesDia.Values.Any(n => n > 0))
                throw new IntegracaoException("MOEDA", "Integração com consumo registrado não pode trocar de moeda. Cadastre uma nova integração para outro contrato financeiro.");
            state.ContratosAnteriores.Add(state.Integracoes[index]);
            if (state.ContratosAnteriores.Count > 20) state.ContratosAnteriores.RemoveAt(0);
            api.Revisao++; api.ModificadaEm = DateTimeOffset.UtcNow;
            state.Integracoes[index] = api;
            return true;
        }, ct);
    }
    public async Task AtualizarOperacaoAsync(Guid id, int revisao, OperacaoApi operacao, CancellationToken ct)
    {
        var api = await ObterAsync(id, ct);
        if (api.Revisao != revisao) throw new IntegracaoException("CONCORRENCIA", "A configuração mudou. Reabra o formulário.");
        var index = api.Contrato.Operacoes.FindIndex(o => o.Id == operacao.Id);
        if (index < 0) throw new KeyNotFoundException("Operação não encontrada.");
        api.Contrato.Operacoes[index] = operacao;
        await SalvarAsync(api, ct);
    }
    public async Task SalvarCredencialAsync(Guid id, string authId, SegredoApi segredo, CancellationToken ct)
    {
        var api = await ObterAsync(id, ct);
        if (!api.Contrato.Autenticacoes.Any(a => a.Id == authId)) throw new IntegracaoException("CREDENCIAL", "A credencial não pertence à integração.");
        if (new[] { segredo.Valor, segredo.Usuario, segredo.ClienteId, segredo.ClienteSegredo }.Any(v => v.Length > 16384 || v.Any(char.IsControl)))
            throw new IntegracaoException("CREDENCIAL", "Credencial inválida ou grande demais.");
        SegredoApi old;
        try { old = await arquivos.LerSegredoAsync(id, authId, ct); }
        catch (IntegracaoException ex) when (ex.Codigo == "SEGREDO_INVALIDO" && new[] { segredo.Valor, segredo.Usuario, segredo.ClienteId, segredo.ClienteSegredo }.Any(v => v.Length > 0)) { old = new(); }
        if (segredo.Valor != "") old.Valor = segredo.Valor;
        if (segredo.Usuario != "") old.Usuario = segredo.Usuario;
        if (segredo.ClienteId != "") old.ClienteId = segredo.ClienteId;
        if (segredo.ClienteSegredo != "") old.ClienteSegredo = segredo.ClienteSegredo;
        old.AccessToken = ""; old.RefreshToken = ""; old.EstadoOAuth = ""; old.VerificadorPkce = "";
        await arquivos.SalvarSegredoAsync(id, authId, old, ct);
    }
    public async Task<string> EstadoCredencialAsync(Guid id, string authId, CancellationToken ct)
    {
        try
        {
            var secret = await arquivos.LerSegredoAsync(id, authId, ct);
            return secret.Valor != "" || secret.ClienteId != "" || secret.AccessToken != "" ? "Configurada" : "Não configurada";
        }
        catch (IntegracaoException ex) when (ex.Codigo == "SEGREDO_INVALIDO") { return "Inválida — recadastre os segredos ou restaure as chaves originais"; }
    }
    public Task<byte[]> BackupAsync(CancellationToken ct) => arquivos.BackupAsync(ct);
    public async Task InvalidarTokensAsync(Guid id, string authId, CancellationToken ct)
    {
        try
        {
            var secret = await arquivos.LerSegredoAsync(id, authId, ct);
            secret.AccessToken = ""; secret.RefreshToken = ""; secret.EstadoOAuth = ""; secret.VerificadorPkce = "";
            await arquivos.SalvarSegredoAsync(id, authId, secret, ct);
        }
        catch (IntegracaoException ex) when (ex.Codigo == "SEGREDO_INVALIDO") { /* Mantém arquivo inválido; o recadastro explícito continua disponível. */ }
    }
    public async Task RemoverCredencialAsync(Guid id, string authId, CancellationToken ct)
    {
        var api = await ObterAsync(id, ct);
        if (!api.Contrato.Autenticacoes.Any(a => a.Id == authId)) throw new IntegracaoException("CREDENCIAL", "Credencial não encontrada.");
        await arquivos.SalvarSegredoAsync(id, authId, new(), ct);
    }
}
