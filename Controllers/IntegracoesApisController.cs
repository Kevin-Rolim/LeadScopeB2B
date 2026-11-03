using System.Text;
using System.Text.Json;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using LeadScopeB2B.ViewModels.Integracoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Admin)]
public sealed class IntegracoesApisController(GerenciadorIntegracaoService service, ICatalogoApis catalogo, FonteDadosService fontes,
    IExecutorApi executor, IMapeadorApi mapper, DestinosHttp rede,
    AutenticacaoIntegracaoService autenticacao, IOptions<MotorIntegracoesOptions> options) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers.CacheControl = "no-store";
        base.OnActionExecuting(context);
    }
    private async Task OpcoesAsync(CancellationToken ct)
    {
        ViewBag.Fontes = await fontes.ListarAsync();
        ViewBag.Redes = new[] { "publica" }.Concat(options.Value.RedesPrivadas.Select(x => x.Id)).ToList();
    }
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.ListarAsync(ct));
    public IActionResult Catalogo() => View(catalogo.Listar());
    [HttpPost]
    public async Task<IActionResult> AdicionarCatalogo(string provedor, CancellationToken ct)
    {
        var contrato = catalogo.Listar().SingleOrDefault(c => c.Provedor == provedor) ?? throw new KeyNotFoundException("Provedor não encontrado.");
        var id = await service.CriarAsync(contrato, 0, ct, provedor);
        return RedirectToAction(nameof(Configurar), new { id });
    }
    [HttpGet]
    public async Task<IActionResult> Nova(CancellationToken ct) { await OpcoesAsync(ct); return View(new NovaApiForm()); }
    [HttpPost, RequestSizeLimit(1048576)]
    public async Task<IActionResult> Nova(NovaApiForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) { await OpcoesAsync(ct); return View(form); }
        if (string.IsNullOrWhiteSpace(form.Contrato.Provedor) && Uri.TryCreate(form.Contrato.UrlBase, UriKind.Absolute, out var endereco)) form.Contrato.Provedor = endereco.Host;
        form.Contrato.Autenticacoes = form.Autenticacao.Tipo == "none" ? [] : [form.Autenticacao];
        form.Operacao.AutenticacaoId = form.Autenticacao.Tipo == "none" ? "" : form.Autenticacao.Id;
        foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(form.Operacao.Caminho, @"\{([^{}]+)\}"))
            if (!form.Operacao.Parametros.Any(p=>p.Nome==m.Groups[1].Value)) form.Operacao.Parametros.Add(new() { Nome=m.Groups[1].Value, Local="path", Obrigatorio=true });
        form.Contrato.Operacoes = [form.Operacao];
        try
        {
            var id = await service.CriarAsync(form.Contrato, form.FonteDadosId, ct);
            return RedirectToAction(nameof(Configurar), new { id });
        }
        catch (IntegracaoException ex) { ModelState.AddModelError("", ex.Message); await OpcoesAsync(ct); return View(form); }
    }
    [HttpGet]
    public async Task<IActionResult> Importar(CancellationToken ct) { await OpcoesAsync(ct); return View(); }
    [HttpPost, RequestSizeLimit(2097152)]
    public async Task<IActionResult> Importar(string? documento, IFormFile? arquivo, string perfilRede = "publica", int fonteDadosId = 0, bool salvar = false, CancellationToken ct = default)
    {
        try
        {
            if (arquivo is not null)
            {
                if (arquivo.Length > 1024 * 1024) throw new IntegracaoException("DOCUMENTO_GRANDE", "Arquivo limitado a 1 MiB.");
                using var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.UTF8, true);
                documento = await reader.ReadToEndAsync(ct);
            }
            var imported = service.Importar(documento ?? "");
            imported.Contrato.PerfilRede = perfilRede;
            ValidacaoContrato.Validar(imported.Contrato, rede);
            if (salvar)
            {
                var id = await service.CriarAsync(imported.Contrato, fonteDadosId, ct);
                TempData["Sucesso"] = "Contrato importado desabilitado. Revise cada operação, limites e credenciais.";
                return RedirectToAction(nameof(Configurar), new { id });
            }
            ViewBag.Importacao = imported;
            ViewBag.Documento = documento;
        }
        catch (IntegracaoException ex) { ModelState.AddModelError("", ex.Message); }
        await OpcoesAsync(ct); return View();
    }
    private async Task<ConfiguracaoApiForm> ConfigForm(Guid id, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct);
        var form = new ConfiguracaoApiForm { Id = id, Revisao = api.Revisao, Nome = api.Contrato.Provedor, UrlBase = api.Contrato.UrlBase,
            PerfilRede = api.Contrato.PerfilRede, Habilitada = api.Habilitada || api.EstadoConexao == "Não conectada", FonteDadosId = api.FonteDadosId, Plano = api.Contrato.Plano, Integracao = api,
            Consumo = (await service.ListarAsync(ct)).Consumo.GetValueOrDefault(id), Automacao = api.Contrato.Operacoes.Any(o=>o.Rotina.Habilitada) };
        foreach (var a in api.Contrato.Autenticacoes)
        {
            form.CredenciaisConfiguradas[a.Id] = await service.EstadoCredencialAsync(id, a.Id, ct);
        }
        return form;
    }
    [HttpGet]
    public async Task<IActionResult> Configurar(Guid id, CancellationToken ct)
    {
        try { await executor.AtualizarConsumoAsync(id, User.UsuarioId(), ct); }
        catch (IntegracaoException) { /* Informação indisponível aparece com a origem/erro armazenados. */ }
        return View(await ConfigForm(id, ct));
    }
    [HttpGet]
    public async Task<IActionResult> Avancado(Guid id, CancellationToken ct) { await OpcoesAsync(ct); return View(await ConfigForm(id, ct)); }
    [HttpPost]
    public async Task<IActionResult> Conectar(ConexaoApiForm form, string? authId, string? valor, string? usuario, string? clienteId, string? clienteSegredo, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Erro"] = "Confira os dados da conexão."; return RedirectToAction(nameof(Configurar), new { id=form.Id }); }
        try
        {
            var result = await service.ConectarAsync(form.Id, form.Revisao, authId,
                authId is null ? null : new SegredoApi { Valor=valor??"", Usuario=usuario??"", ClienteId=clienteId??"", ClienteSegredo=clienteSegredo??"" },
                form.Operacoes, form.Habilitada, form.Automacao, form.AutorizarConsumoCobrado, User.UsuarioId(), ct, form.ParametrosAutomacao);
            TempData[result?.Sucesso == false ? "Erro" : "Sucesso"] = result?.Sucesso == false ? result.Mensagem : "Conexão e operações atualizadas.";
        }
        catch (Exception ex) when (ex is IntegracaoException or ConflitoException) { TempData["Erro"] = ex.Message; }
        return RedirectToAction(nameof(Configurar), new { id=form.Id });
    }
    [HttpPost]
    public async Task<IActionResult> AtualizarConsumo(Guid id, CancellationToken ct)
    {
        try { await executor.AtualizarConsumoAsync(id, User.UsuarioId(), ct); }
        catch (IntegracaoException ex) { TempData["Erro"] = ex.Message; }
        return RedirectToAction(nameof(Configurar), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> Configurar(ConfiguracaoApiForm form, CancellationToken ct)
    {
        var api = await service.ObterAsync(form.Id, ct);
        if (!ModelState.IsValid) { await OpcoesAsync(ct); form.Integracao = api; return View("Avancado",form); }
        api.Revisao = form.Revisao; api.Habilitada = form.Habilitada; api.FonteDadosId = form.FonteDadosId;
        api.Contrato.Provedor = form.Nome; api.Contrato.UrlBase = form.UrlBase; api.Contrato.PerfilRede = form.PerfilRede; api.Contrato.Plano = form.Plano;
        try { await service.SalvarAsync(api, ct); TempData["Sucesso"] = "Configuração salva."; return RedirectToAction(nameof(Configurar), new { id = form.Id }); }
        catch (IntegracaoException ex) { ModelState.AddModelError("", ex.Message); await OpcoesAsync(ct); form.Integracao = api; return View("Avancado",form); }
    }
    [HttpGet]
    public async Task<IActionResult> Operacao(Guid id, string? operacaoId, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct);
        var op = operacaoId is null ? new OperacaoApi { Id = "op_" + Guid.NewGuid().ToString("N")[..8] } : api.Contrato.Operacoes.Single(o => o.Id == operacaoId);
        return View(new OperacaoApiForm { Id = id, Revisao = api.Revisao, Nova = operacaoId is null, Operacao = op, Integracao = api,
            SchemaJson = op.SchemaResposta?.GetRawText() ?? "", CorpoJson = op.Corpo?.GetRawText() ?? "" });
    }
    private static void Jsons(OperacaoApiForm form)
    {
        JsonElement? Parse(string text) { if (string.IsNullOrWhiteSpace(text)) return null; using var json = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 }); return json.RootElement.Clone(); }
        form.Operacao.Corpo = Parse(form.CorpoJson); form.Operacao.SchemaResposta = Parse(form.SchemaJson);
        if (string.IsNullOrWhiteSpace(form.Operacao.ConsultaCota?.CaminhoRpm) && string.IsNullOrWhiteSpace(form.Operacao.ConsultaCota?.CaminhoRestante)) form.Operacao.ConsultaCota = null;
        if (string.IsNullOrWhiteSpace(form.Operacao.Correspondencia?.Parametro) && string.IsNullOrWhiteSpace(form.Operacao.Correspondencia?.Caminho)) form.Operacao.Correspondencia = null;
    }
    [HttpPost, RequestSizeLimit(1048576)]
    public async Task<IActionResult> Operacao(OperacaoApiForm form, CancellationToken ct)
    {
        form.Integracao = await service.ObterAsync(form.Id, ct);
        if (!ModelState.IsValid) return View(nameof(Operacao), form);
        try
        {
            Jsons(form);
            if (form.Nova)
            {
                form.Integracao.Revisao = form.Revisao;
                form.Integracao.Contrato.Operacoes.Add(form.Operacao);
                await service.SalvarAsync(form.Integracao, ct);
            }
            else await service.AtualizarOperacaoAsync(form.Id, form.Revisao, form.Operacao, ct);
            return RedirectToAction(nameof(Configurar), new { id = form.Id });
        }
        catch (IntegracaoException ex) { ModelState.AddModelError("", ex.Message); }
        catch (JsonException) { ModelState.AddModelError("", "Corpo/schema JSON inválido."); }
        return View(form);
    }
    [HttpPost, RequestSizeLimit(1048576)]
    public async Task<IActionResult> Previa(OperacaoApiForm form, CancellationToken ct)
    {
        form.Integracao = await service.ObterAsync(form.Id, ct);
        if (!ModelState.IsValid) return View(nameof(Operacao), form);
        try
        {
            Jsons(form);
            if (Encoding.UTF8.GetByteCount(form.AmostraJson ?? "") > 256 * 1024) throw new IntegracaoException("AMOSTRA", "Amostra limitada a 256 KiB.");
            using var json = JsonDocument.Parse(form.AmostraJson ?? "", new JsonDocumentOptions { MaxDepth = 32 });
            var contract = form.Integracao.Contrato;
            contract.Operacoes = [form.Operacao];
            ValidacaoContrato.Validar(contract, rede);
            form.Previa = mapper.Mapear(json.RootElement, form.Operacao, form.Avisos);
            void Caminhos(JsonElement root, string path, int depth)
            {
                if (depth > 8 || form.CaminhosDisponiveis.Count > 200) return;
                if (root.ValueKind == JsonValueKind.Object) foreach (var p in root.EnumerateObject()) Caminhos(p.Value, path + "[" + JsonSerializer.Serialize(p.Name, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "]", depth + 1);
                else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0) Caminhos(root[0], path + "[0]", depth + 1);
                else form.CaminhosDisponiveis.Add(path);
            }
            var items = JsonSeguro.Selecionar(json.RootElement, form.Operacao.CaminhoItens);
            Caminhos(items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0 ? items[0] : items, "$", 0);
        }
        catch (IntegracaoException ex) { ModelState.AddModelError("", ex.Message); }
        catch (JsonException) { ModelState.AddModelError("", "Amostra JSON inválida."); }
        return View(nameof(Operacao), form);
    }
    [HttpPost]
    public async Task<IActionResult> Credencial(Guid id, string authId, string? valor, string? usuario, string? clienteId, string? clienteSegredo, bool remover, CancellationToken ct)
    {
        if (remover) await service.RemoverCredencialAsync(id, authId, ct);
        else await service.SalvarCredencialAsync(id, authId, new() { Valor = valor ?? "", Usuario = usuario ?? "", ClienteId = clienteId ?? "", ClienteSegredo = clienteSegredo ?? "" }, ct);
        TempData["Sucesso"] = "Credencial atualizada. Segredos não são devolvidos à interface.";
        return RedirectToAction(nameof(Configurar), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> Autenticacao(Guid id, int revisao, AutenticacaoApi autenticacao, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct); api.Revisao = revisao;
        var index = api.Contrato.Autenticacoes.FindIndex(a => a.Id == autenticacao.Id);
        if (index < 0) api.Contrato.Autenticacoes.Add(autenticacao);
        else api.Contrato.Autenticacoes[index] = autenticacao;
        await service.SalvarAsync(api, ct);
        if (index >= 0) await service.InvalidarTokensAsync(id, autenticacao.Id, ct);
        return RedirectToAction(nameof(Configurar), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> OAuth(Guid id, string authId, bool autorizarCusto, bool autorizarCustoDesconhecido, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct);
        return Redirect(await autenticacao.IniciarPkceAsync(api, authId, User.UsuarioId(), ct, autorizarCusto, autorizarCustoDesconhecido));
    }
    [HttpGet]
    public async Task<IActionResult> OAuthCallback(string? state, string? code, string? error, CancellationToken ct)
    {
        if (error is not null) throw new IntegracaoException("OAUTH_RECUSADO", "O provedor recusou a autorização OAuth.");
        await autenticacao.FinalizarPkceAsync(state ?? "", code ?? "", User.UsuarioId(), ct);
        TempData["Sucesso"] = "Conta OAuth conectada."; return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Executar(Guid id, string operacaoId, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct); var op = api.Contrato.Operacoes.Single(o => o.Id == operacaoId);
        return View(new ExecutarApiForm { Id = id, OperacaoId = operacaoId, Integracao = api, Operacao = op,
            Parametros = op.Parametros.Where(p => p.Padrao is not null).ToDictionary(p => p.Nome, p => p.Padrao!) });
    }
    [HttpPost]
    public async Task<IActionResult> Executar(ExecutarApiForm form, CancellationToken ct)
    {
        form.Integracao = await service.ObterAsync(form.Id, ct);
        form.Operacao = form.Integracao.Contrato.Operacoes.Single(o => o.Id == form.OperacaoId);
        if (!ModelState.IsValid) return View(form);
        form.Resultado = await executor.ExecutarAsync(new() { IntegracaoId = form.Id, OperacaoId = form.OperacaoId, UsuarioId = User.UsuarioId(), Parametros = form.Parametros,
            AutorizarEscrita = form.AutorizarEscrita, AutorizarCusto = form.AutorizarCusto, AutorizarCustoDesconhecido = form.AutorizarCustoDesconhecido,
            PersistirEvidencias = form.PersistirEvidencias, EmpresaId = form.EmpresaId, PessoaId = form.PessoaId }, ct);
        return View(form);
    }
    [HttpGet]
    public async Task<IActionResult> Contrato(Guid id, CancellationToken ct)
    {
        var api = await service.ObterAsync(id, ct);
        return File(JsonSerializer.SerializeToUtf8Bytes(api.Contrato, IntegracaoArquivoRepository.Json), "application/json", "contrato-leadscope-v1.json");
    }
    [HttpPost]
    public async Task<IActionResult> Backup(CancellationToken ct) => File(await service.BackupAsync(ct), "application/zip", "integracoes-backup.zip");
}
