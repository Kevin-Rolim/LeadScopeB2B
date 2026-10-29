using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeadScopeB2B.Data;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class UniversalApiTests
{
    private static int count;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception("FALHOU motor universal: " + name); Interlocked.Increment(ref count); Console.WriteLine("OK APIs: " + name); }
    private static async Task Throws<T>(Func<Task> action, string name) where T : Exception
    { try { await action(); } catch (T) { Check(true, name); return; } throw new Exception("FALHOU APIs: " + name); }
    private sealed class RepoFake() : IntegracaoRepository(new SqlLeadScopeB2B(new ConfigurationBuilder().Build()))
    {
        public bool Admin = true;
        public override Task<int> PrepararFonteIntegracaoAsync(string nome, string url, CancellationToken ct) => Task.FromResult(100);
        public override Task<bool> UsuarioPodeIntegrarAsync(int user, CancellationToken ct) => Task.FromResult(Admin && user == 1);
        public override Task<bool> FonteIntegracaoAtivaAsync(int f, CancellationToken ct) => Task.FromResult(true);
        public override Task<List<DestinoDadosApi>> CandidatosIntegracaoAsync(int f, bool p, CancellationToken ct, bool somenteSemEvidencias = true) => Task.FromResult(new List<DestinoDadosApi>());
    }
    private sealed class Factory(DestinosHttp policy, ApiTestServer tls) : IHttpClientFactory, IDisposable
    {
        private readonly SocketsHttpHandler handler = new() { AllowAutoRedirect = false, UseProxy = false, UseCookies = false, ConnectCallback = policy.ConectarAsync,
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == tls.Certificate.GetCertHashString() } };
        public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan };
        public void Dispose() => handler.Dispose();
    }
    private sealed class Context : IDisposable
    {
        private readonly bool limpar;
        private readonly Factory factory;
        public readonly ServiceProvider Provider;
        public readonly string Directory;
        public readonly MotorIntegracoesOptions Options;
        public readonly DestinosHttp Policy;
        public readonly IntegracaoArquivoRepository Store;
        public readonly GerenciadorIntegracaoService Manager;
        public IExecutorApi Executor => Provider.GetRequiredService<IExecutorApi>();
        public Context(ApiTestServer http, ApiTestServer tls, IntegracaoRepository? repo = null, string? directory = null, DestinosHttp? policy = null)
        {
            limpar = directory is null;
            Directory = directory ?? Path.Combine(Path.GetTempPath(), "LeadScope_Apis_" + Guid.NewGuid().ToString("N"));
            Options = new() { Diretorio = Directory, UrlPublicaOAuth = "https://leadscope.example.test", RedesPrivadas = [new() { Id = "testes", Origens = [http.Url, tls.Url], Redes = ["127.0.0.0/8"], PermitirHttp = true }] };
            Policy = policy ?? new DestinosHttp(Microsoft.Extensions.Options.Options.Create(Options));
            var services = new ServiceCollection();
            services.AddLogging().AddSingleton<IOptions<MotorIntegracoesOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
            services.AddSingleton<IDataProtectionProvider>(DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Directory, "keys"))));
            services.AddSingleton(Policy).AddSingleton<IntegracaoArquivoRepository>().AddSingleton<ControleExecucaoApi>();
            services.AddSingleton<IntegracaoRepository>(repo ?? new RepoFake());
            factory = new Factory(Policy, tls); services.AddSingleton<IHttpClientFactory>(factory);
            services.AddSingleton<ConsumoIntegracaoService>().AddSingleton<IConsumoApi>(sp => sp.GetRequiredService<ConsumoIntegracaoService>());
            services.AddSingleton<ApiHttpService>().AddSingleton<AutenticacaoIntegracaoService>().AddSingleton<IAutenticacaoApi>(sp => sp.GetRequiredService<AutenticacaoIntegracaoService>());
            services.AddSingleton<ICatalogoApis, CatalogoApis>();
            services.AddSingleton<IMapeadorApi, MapeadorApi>().AddSingleton<IImportadorApi, ImportadorApi>();
            services.AddTransient<IExecutorApi, ExecutorIntegracaoService>().AddTransient<GerenciadorIntegracaoService>().AddTransient<AutomacaoIntegracaoService>();
            Provider = services.BuildServiceProvider(); Store = Provider.GetRequiredService<IntegracaoArquivoRepository>(); Manager = Provider.GetRequiredService<GerenciadorIntegracaoService>();
        }
        public void Dispose() { Provider.Dispose(); factory.Dispose(); if (limpar && System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
    }
    private static ContratoApi Manifest(string url, string path = "/company") => new() {
        Provedor = "Provedor declarativo", UrlBase = url, PerfilRede = "testes",
        Plano = new() { Sistema = new() { PorMinuto = 100000, Diario = 1000, Mensal = 10000 }, Mensalidade = 0, OrcamentoMensal = 0 },
        Operacoes = [new() { Id = "consulta", Caminho = path, Mapeamentos = [new() { Caminho = "$.company.name", Destino = "Empresa.RazaoSocial", Obrigatorio = true }], Custo = new() { PorRequisicao = 0, PorItem = 0 } }]
    };
    private static async Task<IntegracaoApi> Ativar(Context c, ContratoApi contract, int fonte = 0)
    {
        contract.Plano.Mensalidade ??= 0; // Fixtures locais declaram explicitamente ausência de mensalidade.
        var id = await c.Manager.CriarAsync(contract, fonte, default);
        var api = await c.Manager.ObterAsync(id, default);
        api.Habilitada = true; api.Contrato.Plano.UsoAutorizado = true;
        foreach (var o in api.Contrato.Operacoes) { o.Revisada = true; o.Habilitada = true; }
        await c.Manager.SalvarAsync(api, default);
        return await c.Manager.ObterAsync(id, default);
    }
    private static ExecucaoApi Request(IntegracaoApi api) => new() { IntegracaoId = api.Id, OperacaoId = api.Contrato.Operacoes[0].Id, UsuarioId = 1 };
    public static async Task ExecutarAsync()
    {
        await using var http = new ApiTestServer(); await using var tls = new ApiTestServer(true);
        using var c = new Context(http, tls);
        var api = await Ativar(c, Manifest(http.Url));
        var r = await c.Executor.ExecutarAsync(Request(api), default);
        Check(r.Sucesso && r.Itens.Single()["Empresa.RazaoSocial"] == "Empresa simulada", "A: REST manual sem autenticação funciona com servidor HTTP real autorizado pelo operador");
        var req = Request(api); req.UsuarioId = 999;
        var before = http.Calls;
        Check(!(await c.Executor.ExecutarAsync(req, default)).Sucesso && http.Calls == before, "execução exige administrador válido também no serviço");
        var disabled = await c.Manager.CriarAsync(Manifest(http.Url), 0, default);
        Check((await c.Executor.ExecutarAsync(new() { IntegracaoId = disabled, OperacaoId = "consulta", UsuarioId = 1 }, default)).Codigo == "DESABILITADA", "importação/cadastro começa desabilitado");
        foreach (var mode in new[] { "bearer", "apiKeyHeader", "apiKeyQuery" })
        {
            var contract = Manifest(http.Url, "/auth");
            contract.Autenticacoes = [new() { Id = "credencial", Tipo = mode, Nome = mode == "apiKeyQuery" ? "api_key" : "X-Api-Key" }]; contract.Operacoes[0].AutenticacaoId = "credencial";
            var secured = await Ativar(c, contract);
            await c.Manager.SalvarCredencialAsync(secured.Id, "credencial", new() { Valor = "segredo-local-12345" }, default);
            http.Handler = request => {
                var ok = mode switch { "bearer" => request.Headers.GetValueOrDefault("Authorization") == "Bearer segredo-local-12345", "apiKeyHeader" => request.Headers.GetValueOrDefault("X-Api-Key") == "segredo-local-12345", _ => request.Path.Contains("api_key=segredo-local-12345") };
                return Task.FromResult(new ApiTestResponse(ok ? 200 : 401, """{"company":{"name":"Autenticada"},"access_token":"segredo-local-12345","echo":"segredo-local-12345"}"""));
            };
            r = await c.Executor.ExecutarAsync(Request(secured), default);
            Check(r.Sucesso && !r.AmostraJson.Contains("segredo-local-12345"), "B: autenticação e redação " + mode);
            var publicJson = await File.ReadAllTextAsync(Path.Combine(c.Directory, "configuracao.json"));
            Check(!publicJson.Contains("segredo-local-12345") && !Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(c.Directory, $"segredo-{secured.Id:N}-credencial.bin"))).Contains("segredo-local-12345"), "segredo separado e criptografado " + mode);
            var other = await Ativar(c, contract);
            Check((await c.Executor.ExecutarAsync(Request(other), default)).Codigo == "CREDENCIAL_AUSENTE", "isolamento de credenciais entre integrações " + mode);
        }
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Importada"}}"""));
        var spec = """
            openapi: 3.1.0
            info:
              title: API importada
              version: '1.0'
            servers:
              - url: URL_FIXTURE
            paths:
              /company/{id}:
                get:
                  operationId: lookup
                  parameters:
                    - name: id
                      in: path
                      required: true
                      schema: {type: integer, minimum: 1}
                  responses:
                    '200':
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Company'
                delete:
                  operationId: remove
                  responses:
                    '204': {description: Removed}
            components:
              schemas:
                Company:
                  type: object
                  required: [company]
                  properties:
                    company:
                      type: object
                      properties:
                        name: {type: string}
            """.Replace("URL_FIXTURE", http.Url);
        var imported = new ImportadorApi().Importar(spec);
        Check(imported.Contrato.Operacoes.Count == 2 && imported.Contrato.Operacoes.All(o => !o.Habilitada), "C: OpenAPI YAML 3.1 descobre operações e mantém DELETE desabilitado");
        imported.Contrato.PerfilRede = "testes";
        imported.Contrato.Operacoes = [imported.Contrato.Operacoes.Single(o => o.Id == "lookup")];
        imported.Contrato.Operacoes[0].Custo = new() { PorRequisicao = 0, PorItem = 0 };
        imported.Contrato.Operacoes[0].Mapeamentos = [new() { Caminho = "$.company.name", Destino = "Empresa.RazaoSocial", Obrigatorio = true }];
        imported.Contrato.Plano.Sistema = new() { PorMinuto = 100000, Diario = 100, Mensal = 1000 };
        var importedApi = await Ativar(c, imported.Contrato);
        req = Request(importedApi); req.Parametros["id"] = "7";
        Check((await c.Executor.ExecutarAsync(req, default)).Sucesso && http.Received.Last().Path == "/company/7", "C: contrato OpenAPI importado executa rota tipada e schema resolvido localmente");
        req.Parametros["id"] = "0"; before = http.Calls;
        Check(!(await c.Executor.ExecutarAsync(req, default)).Sucesso && before == http.Calls, "parâmetro fora do schema bloqueia HTTP");
        var jsonOpenApi = JsonSerializer.Serialize(ImportadorApi.LerDocumento(spec.Replace("3.1.0", "3.0.3")));
        Check(new ImportadorApi().Importar(jsonOpenApi).Contrato.Operacoes.Count == 2, "OpenAPI 3.0 JSON suportado");
        var privateContract = Manifest(http.Url, "/private"); privateContract.Provedor = "ERP privado por manifesto";
        var privateImport = new ImportadorApi().Importar(JsonSerializer.Serialize(privateContract, IntegracaoArquivoRepository.Json));
        privateImport.Contrato.Plano.Sistema.PorMinuto = 100000;
        var privateApi = await Ativar(c, privateImport.Contrato);
        Check((await c.Executor.ExecutarAsync(Request(privateApi), default)).Sucesso, "D: API privada conectada por manifesto sem modificar código principal");
        // CNPJAPI usa exatamente o executor e o mapper das demais APIs.
        var cnpj = new CatalogoApis().Listar().Single(x => x.Provedor == "CNPJAPI");
        cnpj.UrlBase = http.Url; cnpj.PerfilRede = "testes"; cnpj.Plano.Sistema.PorMinuto = 100000;
        cnpj.Operacoes = [cnpj.Operacoes[0]]; cnpj.Capacidades.OperacaoCota = ""; cnpj.Operacoes[0].Custo = new() { PorRequisicao = 0, PorItem = 0 };
        var cnpjApi = await Ativar(c, cnpj); await c.Manager.SalvarCredencialAsync(cnpjApi.Id, "chave", new() { Valor = "chave-cnpj-simulada" }, default);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, """{"CNPJ":"00776574000156","RazaoSocial":" Empresa CNPJ ","Endereco":{"UF":"sp"},"QSA":[{"Nome":"Não importar"}]}"""));
        req = Request(cnpjApi); req.Parametros["cnpj"] = "00.776.574/0001-56";
        r = await c.Executor.ExecutarAsync(req, default);
        Check(r.Sucesso && r.Itens[0]["Empresa.Estado"] == "SP" && r.Itens[0].Count == 3 && !r.AmostraJson.Contains("Não importar"), "catálogo CNPJAPI: normalização, identidade e mapeamento sem QSA");
        req.Parametros["cnpj"] = "00000000000000"; before = http.Calls;
        Check(!(await c.Executor.ExecutarAsync(req, default)).Sucesso && before == http.Calls, "CNPJ inválido é bloqueado antes da consulta");
        req.Parametros["cnpj"] = "12ABC34501DE35";
        Check(!(await c.Executor.ExecutarAsync(req, default)).Sucesso, "CNPJAPI numérica não pressupõe suporte alfanumérico");
        http.Handler = _ => Task.FromResult(new ApiTestResponse(429, "{}", new() { ["Retry-After"] = "2" }));
        var limited = await Ativar(c, Manifest(http.Url, "/limited"));
        r = await c.Executor.ExecutarAsync(Request(limited), default);
        Check(r.Codigo == "RATE_LIMIT" && (await c.Store.LerAsync()).Consumo[limited.Id].BloqueadaAte > DateTimeOffset.UtcNow.AddSeconds(1), "E: 429 e Retry-After persistidos");
        before = http.Calls;
        Check((await c.Executor.ExecutarAsync(Request(limited), default)).Codigo == "RETRY_AFTER" && before == http.Calls, "cooldown impede consultas manuais e automáticas antes do prazo");
        var paid = Manifest(http.Url); paid.Operacoes[0].Custo = new() { PorRequisicao = 2, PorItem = 0 }; paid.Plano.OrcamentoMensal = 1;
        var paidApi = await Ativar(c, paid); req = Request(paidApi); req.AutorizarCusto = true;
        Check((await c.Executor.ExecutarAsync(req, default)).Codigo == "ORCAMENTO", "E: custo não pode exceder orçamento");
        var unknown = Manifest(http.Url); unknown.Operacoes[0].Custo = new(); var unknownApi = await Ativar(c, unknown);
        Check((await c.Executor.ExecutarAsync(Request(unknownApi), default)).Codigo == "CUSTO_DESCONHECIDO", "desconhecido não é custo zero");
        var writer = Manifest(http.Url); writer.Operacoes[0].Metodo = "POST"; writer.Operacoes[0].PermitirEscrita = true;
        writer.Operacoes[0].Parametros = [new() { Nome = "name", Local = "body", Obrigatorio = true }, new() { Nome = "count", Local = "body", Tipo = "integer" }];
        var writerApi = await Ativar(c, writer); req = Request(writerApi); req.Parametros = new() { ["name"] = "Criada", ["count"] = "2" };
        Check((await c.Executor.ExecutarAsync(req, default)).Codigo == "ESCRITA_NAO_AUTORIZADA", "escrita externa exige autorização por execução");
        http.Handler = q => { using var body = JsonDocument.Parse(q.Body); Check(body.RootElement.GetProperty("count").GetInt32() == 2, "request body JSON com tipos preservados"); return Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Criada"}}""")); };
        req.AutorizarEscrita = true; Check((await c.Executor.ExecutarAsync(req, default)).Sucesso, "POST autorizado funciona pelo executor compartilhado");
        req.Automatica = true;
        Check((await c.Executor.ExecutarAsync(req, default)).Codigo == "ESCRITA_NAO_AUTORIZADA", "escrita automática bloqueada pelo operador por padrão");
        await ConexaoSimplificadaAsync(http, tls);
        await ExemplosAsync(http, tls);
        await PaginacaoAsync(c, http);
        await QuotasRetriesAsync(http, tls);
        await OAuthAsync(c, http, tls);
        await SegurancaAsync(c, http, tls, spec);
        await AutomacaoAsync(http, tls);
        await PersistenciaAsync(http, tls);
        Console.WriteLine($"SUCESSO APIs: {count} verificações universais sem consumir provedores externos.");
    }
    private static async Task ConexaoSimplificadaAsync(ApiTestServer http, ApiTestServer tls)
    {
        using var c = new Context(http, tls);
        var contrato = new CatalogoApis().Listar().Single(x=>x.Provedor=="CNPJAPI");
        contrato.UrlBase = http.Url; contrato.PerfilRede = "testes";
        contrato.Plano.Sistema.PorMinuto = 100000; contrato.Plano.Sistema.Diario = 1; // Só o fixture evita esperas longas.
        var quotaCalls = 0; var empresas = 0; var saldo = 97; var usado = 3;
        http.Handler = q => {
            Check(q.Headers.GetValueOrDefault("Authorization")=="Bearer chave-simples-teste", "conexão simples utiliza autenticação do catálogo");
            if (q.Path=="/cota") { quotaCalls++; return Task.FromResult(new ApiTestResponse(200, JsonSerializer.Serialize(new { competencia="2026-10", usado, limite=100, restante=saldo, rpm=30 }))); }
            empresas++; return Task.FromResult(new ApiTestResponse(200, """{"CNPJ":"00776574000156","RazaoSocial":"Empresa consultada"}"""));
        };
        var id = await c.Manager.CriarAsync(contrato, 0, default, "CNPJAPI");
        var api = await c.Manager.ObterAsync(id, default);
        var connected = await c.Manager.ConectarAsync(id, api.Revisao, "chave", new() { Valor="chave-simples-teste" }, ["consultarCnpj"], true, false, false, 1, default);
        api = await c.Manager.ObterAsync(id, default); var usage=(await c.Store.LerAsync()).Consumo[id];
        Check(connected?.Sucesso==true && quotaCalls==1 && empresas==0, "catálogo conecta somente com chave, sem consulta de negócio implícita");
        Check(api.FonteDadosId==100 && api.Contrato.Plano.Mensalidade is null && api.Contrato.Plano.OrcamentoMensal is null && api.Contrato.Plano.Nome=="Não informado", "fonte gerenciada automaticamente; nenhum plano ou preço inferido");
        Check(usage.Endpoint is { Usado:3, Limite:100, Restante:97, Rpm:30, Competencia:"2026-10", RenovaEm:null }, "CNPJAPI descobre competência, uso, limite, saldo e RPM sem inventar fuso/renovação");
        await c.Executor.AtualizarConsumoAsync(id, 1, default);
        Check(quotaCalls==1, "cache compartilhado impede consultas de cota duplicadas");
        var req = Request(api); req.OperacaoId="consultarCnpj"; req.Parametros["cnpj"]="00776574000156"; req.AutorizarCustoDesconhecido=true;
        await c.Store.AlterarAsync(s=>{s.Consumo[id].ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        Check((await c.Executor.ExecutarAsync(req,default)).Sucesso && quotaCalls==1 && empresas==1, "consulta com custo desconhecido autorizado não exige mensalidade ou orçamento");
        usage=(await c.Store.LerAsync()).Consumo[id];
        Check(usage.Endpoint!.Restante==97 && usage.RestanteProvedor==96 && usage.ConsumoCustoDesconhecido.Values.Sum()==1, "saldo confirmado e projeção local permanecem distintos; desconhecido não é preço zero");
        Check(usage.ConsultasGerenciaisDia.Values.Sum()==1 && usage.RequisicoesDia.Values.Sum()==2, "cota oficial sem débito não esgota o orçamento local de consultas de negócio");
        api=await c.Manager.ObterAsync(id,default); api.Contrato.Plano.Sistema.Diario=20; await c.Manager.SalvarAsync(api,default);
        await Throws<IntegracaoException>(()=>c.Manager.ConectarAsync(id,api.Revisao,null,null,["consultarCnpj"],true,true,false,1,default), "automação de custo desconhecido exige autorização específica");
        api=await c.Manager.ObterAsync(id,default);
        await c.Store.AlterarAsync(s=>{s.Consumo[id].ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        await c.Manager.ConectarAsync(id,api.Revisao,null,null,["consultarCnpj"],true,true,true,1,default);
        api=await c.Manager.ObterAsync(id,default);
        Check(api.AutomacaoPelaInterface && api.Contrato.Operacoes.Single(o=>o.Id=="consultarCnpj").Rotina is {Habilitada:true,UsuarioId:1,AutorizarCustoDesconhecido:true}, "interruptor prepara rotina e responsável sem configuração técnica do usuário");
        saldo=80; usado=20;
        await c.Store.AlterarAsync(s=>{s.Consumo[id].UltimaTentativaCota=DateTimeOffset.UtcNow.AddHours(-1);s.Consumo[id].ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        await c.Executor.AtualizarConsumosPendentesAsync(default);
        Check((await c.Store.LerAsync()).Consumo[id].Endpoint is {Usado:20,Restante:80} && quotaCalls==3, "consumo é atualizado periodicamente pelo mesmo motor de automação");
        await c.Store.AlterarAsync(s=>{s.Consumo[id].RestanteProvedor=0;return true;});
        Check((await c.Executor.ExecutarAsync(req,default)).Codigo=="COTA_PROVEDOR", "saldo confirmado/projetado esgotado impede consultas");
        // Mesmo contrato: ilimitado só é aceito quando a semântica foi explicitamente declarada.
        http.Handler = _=>Task.FromResult(new ApiTestResponse(200,"""{"competencia":"2026-11","usado":0,"limite":0,"restante":null,"rpm":30}"""));
        await c.Store.AlterarAsync(s=>{s.Consumo[id].UltimaTentativaCota=DateTimeOffset.UtcNow.AddHours(-1);s.Consumo[id].ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        await c.Executor.AtualizarConsumoAsync(id,1,default);
        Check((await c.Store.LerAsync()).Consumo[id].Endpoint is {SemLimite:true,Competencia:"2026-11",Limite:0} && (await c.Store.LerAsync()).Consumo[id].RestanteProvedor is null, "limite zero e saldo nulo respeitam semântica oficial sem inferir plano");
        http.Handler=_=>Task.FromResult(new ApiTestResponse(401,"{}"));
        await c.Store.AlterarAsync(s=>{s.Consumo[id].UltimaTentativaCota=DateTimeOffset.UtcNow.AddHours(-1);s.Consumo[id].ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        var falhaCota=await c.Executor.ExecutarAsync(req,default);var chamadasCota=http.Calls;
        var falhaCache=await c.Executor.ExecutarAsync(req,default);
        Check(falhaCota.Codigo=="CREDENCIAL_INVALIDA" && falhaCache.Codigo=="CREDENCIAL_INVALIDA" && http.Calls==chamadasCota,"falha recente de autenticação da cota não libera consulta empresarial pelo cache");
        api=await c.Manager.ObterAsync(id,default);
        await c.Manager.ConectarAsync(id,api.Revisao,null,null,["consultarCnpj"],false,false,false,1,default);
        var before=quotaCalls; await c.Executor.AtualizarConsumosPendentesAsync(default);
        Check(!((await c.Manager.ObterAsync(id,default)).AutomacaoPelaInterface) && quotaCalls==before, "desativação interrompe descoberta periódica e automação");
        // REST comum sem adaptador, custos ou limites conhecidos.
        var externa=Manifest(http.Url); externa.Plano=new() { Sistema=new() }; externa.Operacoes[0].Custo=new();
        http.Handler=_=>Task.FromResult(new ApiTestResponse(200,"""{"company":{"name":"REST externa"}}"""));
        id=await c.Manager.CriarAsync(externa,0,default);api=await c.Manager.ObterAsync(id,default);
        await c.Manager.ConectarAsync(id,api.Revisao,null,null,["consulta"],true,false,false,1,default);api=await c.Manager.ObterAsync(id,default);
        req=Request(api);req.AutorizarCustoDesconhecido=true;
        Check((await c.Executor.ExecutarAsync(req,default)).Sucesso && (await c.Store.LerAsync()).Consumo[id].Endpoint is null, "REST externa funciona sem adaptador, limites cadastrados ou estrutura financeira");
        Check((await c.Store.LerAsync()).Consumo[id].ProximaRequisicao > DateTimeOffset.UtcNow.AddSeconds(8), "limite desconhecido usa política local conservadora, sem alegar confirmação externa");
        var headers=Manifest(http.Url); var headersId=await Ativar(c,headers);
        http.Handler=_=>Task.FromResult(new ApiTestResponse(200,"""{"company":{"name":"Headers"}}""",new() { ["X-RateLimit-Limit-Minute"]="30", ["RateLimit-Policy"]="2;w=60", ["RateLimit-Remaining"]="0", ["RateLimit-Reset"]="1" }));
        Check((await c.Executor.ExecutarAsync(Request(headersId),default)).Sucesso && (await c.Store.LerAsync()).Consumo[headersId.Id].Headers is {Rpm:30,Restante:0,RenovaEm:not null}, "headers com semântica conhecida descobrem frequência, saldo e janela");
        Check((await c.Executor.ExecutarAsync(Request(headersId),default)).Codigo is "JANELA_PROVEDOR" or "COTA_PROVEDOR", "janela conhecida em headers suspende novas chamadas");
        await c.Store.AlterarAsync(s=>{var u=s.Consumo[headersId.Id];u.JanelaAte=DateTimeOffset.UtcNow.AddSeconds(-1);u.Headers!.RenovaEm=u.JanelaAte;u.ProximaRequisicao=DateTimeOffset.MinValue;return true;});
        http.Handler=_=>Task.FromResult(new ApiTestResponse(200,"""{"company":{"name":"Retomada"}}"""));
        Check((await c.Executor.ExecutarAsync(Request(headersId),default)).Sucesso, "renovação explícita permite retomada sem presumir calendário do provedor");
    }
    private static async Task ExemplosAsync(ApiTestServer http, ApiTestServer tls)
    {
        using var c = new Context(http, tls);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var file = await File.ReadAllTextAsync(Path.Combine(root, "Docs/Exemplos/contrato-privado-1.0.json"));
        var imported = new ImportadorApi().Importar(file);
        imported.Contrato.UrlBase = http.Url + "/v1"; imported.Contrato.PerfilRede = "testes"; imported.Contrato.Plano.Sistema.PorMinuto = 100000;
        var api = await Ativar(c, imported.Contrato);
        await c.Manager.SalvarCredencialAsync(api.Id, "erp", new() { Valor = "chave-demo-local" }, default);
        http.Handler = q => Task.FromResult(new ApiTestResponse(q.Headers.GetValueOrDefault("X-Integration-Key") == "chave-demo-local" ? 200 : 401,
            """{"registration":"00776574000156","organization":{"legalName":"Empresa fictícia de demonstração"},"location":{"city":"São Paulo","state":"SP"}}"""));
        var req = Request(api); req.Parametros["registration"] = "00776574000156";
        var r = await c.Executor.ExecutarAsync(req, default);
        Check(r.Sucesso && r.Itens.Single().Count == 4, "exemplo privado publicado importa e executa contrato/schema/mapeamento sem código C# do provedor");
        var openapi = new ImportadorApi().Importar(await File.ReadAllTextAsync(Path.Combine(root, "Docs/Exemplos/openapi-privada-3.1.yaml")));
        Check(openapi.Contrato.Operacoes.Count == 1 && openapi.Contrato.Autenticacoes.Single().Tipo == "apiKeyHeader", "exemplo OpenAPI YAML publicado é importável com descoberta de autenticação");
        openapi.Contrato.UrlBase = http.Url + "/v1"; openapi.Contrato.PerfilRede = "testes"; openapi.Contrato.Plano.Sistema.PorMinuto = 100000;
        openapi.Contrato.Operacoes[0].Custo = new() { PorRequisicao = 0, PorItem = 0 };
        openapi.Contrato.Operacoes[0].Mapeamentos = imported.Contrato.Operacoes[0].Mapeamentos;
        var fromOpenApi = await Ativar(c, openapi.Contrato);
        await c.Manager.SalvarCredencialAsync(fromOpenApi.Id, "erp", new() { Valor = "chave-demo-local" }, default);
        var openReq = Request(fromOpenApi); openReq.Parametros["registration"] = "00776574000156";
        Check((await c.Executor.ExecutarAsync(openReq, default)).Sucesso, "exemplo OpenAPI autenticado executa após configurar credencial e mapeamento");
    }
    private static async Task PaginacaoAsync(Context c, ApiTestServer http)
    {
        foreach (var type in new[] { "page", "offset", "cursor" })
        {
            var call = 0;
            http.Handler = request => { call++; return Task.FromResult(new ApiTestResponse(200, call == 1 ? """{"items":[{"name":"Página 1"}],"next":"cursor2"}""" : """{"items":[],"next":null}""")); };
            var manifest = Manifest(http.Url, "/pages");
            var op = manifest.Operacoes[0]; op.CaminhoItens = "$.items"; op.Mapeamentos[0].Caminho = "$.name";
            op.Paginacao = new() { Tipo = type, Inicio = type == "offset" ? 0 : 1, Tamanho = 1, MaxPaginas = 3, Parametro = "page", CaminhoProximoCursor = "$.next" };
            var api = await Ativar(c, manifest);
            var r = await c.Executor.ExecutarAsync(Request(api), default);
            Check(r.Sucesso && r.Itens.Count == 1 && call == 2, "paginação limitada " + type);
            if (type == "cursor") Check(http.Received.Last().Path.Contains("page=cursor2"), "cursor é parâmetro, nunca URL de destino");
        }
    }
    private static async Task QuotasRetriesAsync(ApiTestServer http, ApiTestServer tls)
    {
        using var c = new Context(http, tls);
        var manifest = Manifest(http.Url, "/quota");
        manifest.Operacoes[0].ConsultaCota = new() { CaminhoRpm = "$.rpm", CaminhoRestante = "$.restante", RestanteNuloSemLimite = true };
        manifest.Operacoes[0].Mapeamentos = []; manifest.Operacoes[0].Custo.CreditosPorUnidade = 0;
        var lookup = Manifest(http.Url).Operacoes[0]; lookup.Id = "lookup"; lookup.Revisada = true; lookup.Habilitada = true;
        manifest.Operacoes.Add(lookup);
        var api = await Ativar(c, manifest);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, "{\"rpm\":100000,\"restante\":0}"));
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso, "adaptador declarativo consulta cota no mesmo executor");
        var req = Request(api); req.OperacaoId = "lookup";
        Check((await c.Executor.ExecutarAsync(req, default)).Codigo == "COTA_PROVEDOR", "cota informada pelo provedor restringe consultas");
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, "{\"rpm\":100000,\"restante\":null}"));
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso && (await c.Store.LerAsync()).Consumo[api.Id].RestanteProvedor is null, "consulta de cota permite atualização de zero e semântica documentada de nulo");
        api = await c.Manager.ObterAsync(api.Id, default);
        api.Contrato.Operacoes[0].ConsultaCota!.UnidadeRestante = "credito";
        api.Contrato.Operacoes.Single(o => o.Id == "lookup").Custo.CreditosPorUnidade = 5;
        await c.Manager.SalvarAsync(api, default);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, "{\"rpm\":100000,\"restante\":3}"));
        await c.Executor.ExecutarAsync(Request(api), default);
        Check((await c.Executor.ExecutarAsync(req, default)).Codigo == "COTA_PROVEDOR", "cota oficial em créditos respeita unidades necessárias por operação");
        http.Handler = q => Task.FromResult(new ApiTestResponse(200, q.Path.StartsWith("/quota") ? "{\"rpm\":100000,\"restante\":8}" : "{\"company\":{\"name\":\"Créditos\"}}"));
        await c.Executor.ExecutarAsync(Request(api), default);
        Check((await c.Executor.ExecutarAsync(req, default)).Sucesso && (await c.Store.LerAsync()).Consumo[api.Id].RestanteProvedor == 3 && (await c.Store.LerAsync()).Consumo[api.Id].UnidadeCotaProvedor == "credito", "consumo conserva unidade oficial quando a resposta seguinte não contém header de cota");
        var subscription = Manifest(http.Url); subscription.Plano.Mensalidade = 10; subscription.Plano.OrcamentoMensal = null;
        var subscribed = await Ativar(c, subscription);
        Check((await c.Executor.ExecutarAsync(Request(subscribed), default)).Sucesso, "mensalidade não é condição para executar uma consulta sem custo incremental");
        http.Handler = _ => Task.FromResult(new ApiTestResponse(502, "{}"));
        var retry = Manifest(http.Url); retry.Operacoes[0].TentativasAdicionais = 2;
        var retried = await Ativar(c, retry); var before = http.Calls;
        Check(!(await c.Executor.ExecutarAsync(Request(retried), default)).Sucesso && http.Calls == before + 3, "leitura com retries explícitos para após o máximo de três tentativas");
        retry.Operacoes[0].Metodo = "POST"; retry.Operacoes[0].PermitirEscrita = true;
        var write = await Ativar(c, retry); var writeReq = Request(write); writeReq.AutorizarEscrita = true; before = http.Calls;
        Check(!(await c.Executor.ExecutarAsync(writeReq, default)).Sucesso && http.Calls == before + 1, "escrita não é repetida mesmo com retries configurados");
        var credits = Manifest(http.Url); credits.Plano.Sistema.CreditosMensais = 0;
        var noCredits = await Ativar(c, credits);
        Check((await c.Executor.ExecutarAsync(Request(noCredits), default)).Codigo == "CREDITOS", "saldo de créditos bloqueia consumo antes do HTTP");
        var after = DateTimeOffset.UtcNow.AddMinutes(2);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(429, "{}", new() { ["Retry-After"] = after.ToString("r", System.Globalization.CultureInfo.InvariantCulture) }));
        var dates = await Ativar(c, Manifest(http.Url));
        await c.Executor.ExecutarAsync(Request(dates), default);
        Check((await c.Store.LerAsync()).Consumo[dates.Id].BloqueadaAte > DateTimeOffset.UtcNow.AddSeconds(110), "Retry-After em data HTTP é persistido corretamente");
    }
    private static async Task OAuthAsync(Context unused, ApiTestServer http, ApiTestServer tls)
    {
        using var c = new Context(http, tls);
        var tokens = 0; string verifier = "";
        tls.Handler = request =>
        {
            if (request.Path.StartsWith("/token"))
            {
                tokens++;
                if (request.Body.Contains("code_verifier=")) verifier = Uri.UnescapeDataString(request.Body.Split('&').Single(s => s.StartsWith("code_verifier="))[14..]);
                return Task.FromResult(new ApiTestResponse(200, """{"access_token":"oauth-local-access","token_type":"Bearer","expires_in":3600,"refresh_token":"oauth-local-refresh"}"""));
            }
            return Task.FromResult(new ApiTestResponse(request.Headers.GetValueOrDefault("Authorization") == "Bearer oauth-local-access" ? 200 : 401, """{"company":{"name":"OAuth funcionando"}}"""));
        };
        var manifest = Manifest(tls.Url, "/oauth-resource");
        manifest.Autenticacoes = [new() { Id = "oauth", Tipo = "clientCredentials", TokenUrl = tls.Url + "/token", Escopos = "read" }]; manifest.Operacoes[0].AutenticacaoId = "oauth";
        var api = await Ativar(c, manifest);
        await c.Manager.SalvarCredencialAsync(api.Id, "oauth", new() { ClienteId = "cliente-local", ClienteSegredo = "cliente-segredo-local" }, default);
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso && tokens == 1, "OAuth Client Credentials real com TLS, orçamento e autenticação compartilhados");
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso && tokens == 1, "OAuth usa token em cache sem nova troca");
        var secret = await c.Store.LerSegredoAsync(api.Id, "oauth"); secret.ExpiraEm = DateTimeOffset.UtcNow.AddMinutes(-1); await c.Store.SalvarSegredoAsync(api.Id, "oauth", secret);
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso && tokens == 2, "token expirado é renovado");
        var pkce = Manifest(tls.Url, "/oauth-resource"); pkce.Autenticacoes = [new() { Id = "pkce", Tipo = "authorizationCode", TokenUrl = tls.Url + "/token", AutorizacaoUrl = tls.Url + "/authorize", Escopos = "read" }]; pkce.Operacoes[0].AutenticacaoId = "pkce";
        var pkceApi = await Ativar(c, pkce); await c.Manager.SalvarCredencialAsync(pkceApi.Id, "pkce", new() { ClienteId = "cliente-publico" }, default);
        var auth = c.Provider.GetRequiredService<AutenticacaoIntegracaoService>();
        var authorizationUrl = await auth.IniciarPkceAsync(pkceApi, "pkce", 1, default);
        var query = new Uri(authorizationUrl).Query[1..].Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
        Check(query["code_challenge_method"] == "S256" && query["code_challenge"].Length >= 40, "Authorization Code com desafio PKCE S256");
        await Throws<IntegracaoException>(() => auth.FinalizarPkceAsync(query["state"], "code-local", 2, default), "estado OAuth é vinculado ao administrador");
        await auth.FinalizarPkceAsync(query["state"], "code-local", 1, default);
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Check(challenge == query["code_challenge"] && (await c.Executor.ExecutarAsync(Request(pkceApi), default)).Sucesso, "troca de código usa verificador PKCE correto e executa API");
        await Throws<IntegracaoException>(() => auth.FinalizarPkceAsync(query["state"], "code-local", 1, default), "estado OAuth de uso único");
        secret = await c.Store.LerSegredoAsync(pkceApi.Id, "pkce"); secret.ExpiraEm = DateTimeOffset.UtcNow.AddMinutes(-1); await c.Store.SalvarSegredoAsync(pkceApi.Id, "pkce", secret);
        var t = tokens;
        Check((await c.Executor.ExecutarAsync(Request(pkceApi), default)).Sucesso && tokens == t + 1 && tls.Received.Any(r => r.Body.Contains("grant_type=refresh_token")), "refresh token do Authorization Code é renovado");
        var basic = Manifest(tls.Url, "/basic"); basic.Autenticacoes = [new() { Id = "basic", Tipo = "basic" }]; basic.Operacoes[0].AutenticacaoId = "basic";
        tls.Handler = r => Task.FromResult(new ApiTestResponse(r.Headers.GetValueOrDefault("Authorization") == "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("usuario:senha-local")) ? 200 : 401, """{"company":{"name":"Basic TLS"}}"""));
        var basicApi = await Ativar(c, basic); await c.Manager.SalvarCredencialAsync(basicApi.Id, "basic", new() { Usuario = "usuario", Valor = "senha-local" }, default);
        Check((await c.Executor.ExecutarAsync(Request(basicApi), default)).Sucesso, "HTTP Basic suportado exclusivamente em HTTPS");
        tls.Handler = q => Task.FromResult(new ApiTestResponse(200, JsonSerializer.Serialize(new { company = new { name = q.Headers["Authorization"] } })));
        var echoed = await c.Executor.ExecutarAsync(Request(basicApi), default);
        Check(echoed.Codigo == "SEGREDO_NA_RESPOSTA" && echoed.Itens.Count == 0, "credencial Basic codificada devolvida pelo provedor não chega à interface ou ao mapeamento persistido");
        basic.UrlBase = http.Url;
        await Throws<IntegracaoException>(() => c.Manager.CriarAsync(basic, 0, default), "Basic sobre HTTP é recusado mesmo em rede privada autorizada");
    }
    private static async Task SegurancaAsync(Context unused, ApiTestServer http, ApiTestServer tls, string spec)
    {
        using var c = new Context(http, tls);
        foreach (var url in new[] { "https://127.0.0.1", "https://[::1]", "https://10.0.0.1", "https://169.254.169.254", "https://100.64.0.1", "http://example.com", "file:///etc/passwd", "https://user:password@example.com", "https://[::ffff:127.0.0.1]", "https://192.168.1.1" })
        {
            var manifest = Manifest(url); manifest.PerfilRede = "publica";
            await Throws<IntegracaoException>(() => c.Manager.CriarAsync(manifest, 0, default), "G: SSRF/URL bloqueada " + url.Split('@').Last());
        }
        var unauthorized = Manifest(http.Url); unauthorized.PerfilRede = "redeInventada";
        await Throws<IntegracaoException>(() => c.Manager.CriarAsync(unauthorized, 0, default), "rede interna depende do operador, não do cadastro");
        Check(!c.Policy.EnderecoPermitido(IPAddress.Parse("10.0.0.1"), "testes"), "perfil não concede acesso irrestrito à rede privada");
        foreach (var text in new[] { "{", "{\"versaoContrato\":\"99\"}", spec.Replace("3.1.0", "3.2.0"), spec.Replace("#/components/schemas/Company", "file:///etc/passwd"), "a: &a [*a]", "a: !CustomType value", "a: 1\n---\nb: 2", spec.Replace("#/components/schemas/Company", "#/components/schemas/Loop") + "\n" })
        {
            await Throws<IntegracaoException>(() => { var imported = new ImportadorApi().Importar(text); ValidacaoContrato.Validar(imported.Contrato, c.Policy); return Task.CompletedTask; }, "G: contrato malformado/versão/referência/tag não suportados");
        }
        foreach (var path in new[] { "$..password", "$.items[?(@.id)]", "$.constructor()", "$.items[*]" })
            await Throws<IntegracaoException>(() => { JsonSeguro.ValidarCaminho(path); return Task.CompletedTask; }, "JSONPath não executa expressões " + path);
        var definitions = new Dictionary<string, object> { ["leaf"] = new { type = "string", description = new string('x', 200000) } };
        for (var level = 1; level <= 7; level++)
        {
            var previous = level == 1 ? "leaf" : "n" + (level - 1);
            definitions["n" + level] = new { type = "object", properties = new Dictionary<string, object> {
                ["a"] = new Dictionary<string, string> { ["$ref"] = "#/components/schemas/" + previous },
                ["b"] = new Dictionary<string, string> { ["$ref"] = "#/components/schemas/" + previous }
            } };
        }
        var expansion = JsonSerializer.Serialize(new { openapi = "3.1.0", info = new { title = "Limite" }, servers = new[] { new { url = http.Url } },
            paths = new Dictionary<string, object> { ["/data"] = new { get = new { responses = new Dictionary<string, object> { ["200"] = new { content = new Dictionary<string, object> { ["application/json"] = new { schema = new Dictionary<string, string> { ["$ref"] = "#/components/schemas/n7" } } } } } } } },
            components = new { schemas = definitions } });
        await Throws<IntegracaoException>(() => { new ImportadorApi().Importar(expansion); return Task.CompletedTask; }, "expansão de referências locais é limitada mesmo com documento pequeno");
        using (var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":{\"type\":\"string\"}}"))
        using (var invalid = JsonDocument.Parse("{\"field\":123}"))
            await Throws<IntegracaoException>(() => { JsonSeguro.ValidarResposta(invalid.RootElement, schema.RootElement); return Task.CompletedTask; }, "schema valida additionalProperties sem exigir properties fixas");
        // Simula DNS público na validação e loopback no instante da conexão. O callback deve bloquear o rebinding.
        var resolves = 0;
        var rebindingPolicy = new DestinosHttp(Options.Create(new MotorIntegracoesOptions()), (_, _) => Task.FromResult(new[] { ++resolves == 1 ? IPAddress.Parse("8.8.8.8") : IPAddress.Loopback }));
        using (var rb = new Context(http, tls, policy: rebindingPolicy))
        {
            var m = Manifest("https://rebinding.example.test"); m.PerfilRede = "publica"; var a = await Ativar(rb, m);
            var result = await rb.Executor.ExecutarAsync(Request(a), default);
            Check(!result.Sucesso && resolves >= 2, "G: DNS rebinding bloqueado no endereço efetivamente usado pela conexão");
        }
        var before = http.Calls;
        http.Handler = _ => Task.FromResult(new ApiTestResponse(302, "", new() { ["Location"] = "http://169.254.169.254/latest/meta-data" }));
        var api = await Ativar(c, Manifest(http.Url));
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Codigo == "REDIRECIONAMENTO_BLOQUEADO" && http.Calls == before + 1, "G: redirecionamento não é seguido");
        foreach (var code in new[] { 400, 401, 403, 404, 503 })
        {
            http.Handler = _ => Task.FromResult(new ApiTestResponse(code, "{\"secret\":\"não devolver\"}"));
            api = await Ativar(c, Manifest(http.Url)); var r = await c.Executor.ExecutarAsync(Request(api), default);
            Check(r.HttpStatus == code && !r.Sucesso && r.AmostraJson == "", "HTTP " + code + " sem expor corpo de erro");
        }
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, "{"));
        api = await Ativar(c, Manifest(http.Url)); Check((await c.Executor.ExecutarAsync(Request(api), default)).Codigo == "JSON_INVALIDO", "JSON inválido não gera evidência");
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, new string('x', 1024 * 1024 + 1)));
        api = await Ativar(c, Manifest(http.Url)); Check((await c.Executor.ExecutarAsync(Request(api), default)).Codigo == "RESPOSTA_GRANDE", "tamanho de resposta limitado");
        http.Handler = async r => { await Task.Delay(2000, r.Cancellation); return new ApiTestResponse(200, "{}"); };
        var slow = Manifest(http.Url); slow.Operacoes[0].TimeoutSegundos = 1; api = await Ativar(c, slow);
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Codigo == "TIMEOUT", "timeout de requisição");
        using(var cancel = new CancellationTokenSource(100)) await Throws<OperationCanceledException>(() => c.Executor.ExecutarAsync(Request(api), cancel.Token), "cancelamento de requisição propagado");
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Reserva"}}"""));
        var daily = Manifest(http.Url); daily.Plano.Sistema.Diario = 1; api = await Ativar(c, daily);
        Check((await c.Executor.ExecutarAsync(Request(api), default)).Sucesso && (await c.Executor.ExecutarAsync(Request(api), default)).Codigo == "COTA_LOCAL", "cota diária inclui consultas manuais");
        var unknown = Manifest(http.Url); unknown.Operacoes[0].Custo = new() { ReservaCustoDesconhecido = 2 }; unknown.Plano.OrcamentoMensal = 10;
        api = await Ativar(c, unknown); var req = Request(api); req.AutorizarCusto = true; req.AutorizarCustoDesconhecido = true;
        Check((await c.Executor.ExecutarAsync(req, default)).Sucesso && (await c.Store.LerAsync()).Consumo[api.Id].CustoMes.Values.Single() == 2, "custo desconhecido exige autorização e reserva persistente");
    }
    private static async Task AutomacaoAsync(ApiTestServer http, ApiTestServer tls)
    {
        using var c = new Context(http, tls);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Agendada genérica"}}"""));
        var api = await Ativar(c, Manifest(http.Url));
        var workerFactory = c.Provider.GetRequiredService<IServiceScopeFactory>();
        using (var disabled = new ColetaAutomaticaWorker(workerFactory, Options.Create(c.Options), NullLogger<ColetaAutomaticaWorker>.Instance))
        {
            var before = http.Calls; await disabled.StartAsync(default); await disabled.StopAsync(default);
            Check(http.Calls == before, "F: worker desabilitado não faz HTTP");
        }
        api.Contrato.Operacoes[0].Rotina = new() { Habilitada = true, UsuarioId = 1, IntervaloMinutos = 1, LimitePorExecucao = 1 };
        await c.Manager.SalvarAsync(api, default);
        c.Options.AutomacaoHabilitada = true; c.Options.IntervaloVerificacaoSegundos = 1;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        http.Handler = _ => { started.TrySetResult(); return Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Genérica"}}""")); };
        using(var worker = new ColetaAutomaticaWorker(workerFactory, Options.Create(c.Options), NullLogger<ColetaAutomaticaWorker>.Instance))
        {
            await worker.StartAsync(default); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Check((await c.Store.LerAsync()).Consumo[api.Id].ProximaRotina.ContainsKey("consulta"), "F: BackgroundService executa contrato genérico e persiste próximo ciclo");
        var beforeNext = http.Calls; await c.Provider.GetRequiredService<AutomacaoIntegracaoService>().ExecutarPendentesAsync(default);
        Check(http.Calls == beforeNext, "F: ciclo reservado não é duplicado após reinício do worker");
        var empty=await Ativar(c,Manifest(http.Url),100);
        empty.Contrato.Operacoes[0].Rotina=new(){Habilitada=true,UsuarioId=1,Criterio="empresasSemEvidencias",IntervaloMinutos=720};
        await c.Manager.SalvarAsync(empty,default);
        var beforeEmpty=http.Calls;
        await c.Provider.GetRequiredService<AutomacaoIntegracaoService>().ExecutarPendentesAsync(default);
        Check(http.Calls==beforeEmpty && (await c.Store.LerAsync()).Consumo[empty.Id].ProximaRotina["consulta"]<DateTimeOffset.UtcNow.AddMinutes(2),"F: ciclo sem candidatos verifica novos cadastros em um minuto sem consumir API");
        api = await c.Manager.ObterAsync(api.Id, default); api.Contrato.Operacoes[0].Rotina.Habilitada = false; await c.Manager.SalvarAsync(api, default);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        http.Handler = async r => { pending.TrySetResult(); await Task.Delay(Timeout.Infinite, r.Cancellation); return new ApiTestResponse(200, "{}"); };
        using var cancel = new CancellationTokenSource(); var task = c.Executor.ExecutarAsync(Request(api), cancel.Token);
        await pending.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel();
        await Throws<OperationCanceledException>(() => task, "F: cancelamento interrompe execução em andamento sem persistir dados parciais");
    }
    private static async Task PersistenciaAsync(ApiTestServer http, ApiTestServer tls)
    {
        var directory = Path.Combine(Path.GetTempPath(), "LeadScope_Restart_" + Guid.NewGuid().ToString("N"));
        Guid id; byte[] backup;
        using(var c = new Context(http, tls, directory: directory))
        {
            var api = await Ativar(c, Manifest(http.Url)); id = api.Id;
            await c.Store.SalvarSegredoAsync(id, "cred", new() { Valor = "segredo-backup-local" });
            // O backup inclui segredos separados, independentemente da presença de credenciais no formulário.
            await c.Store.AlterarAsync(s => { s.Consumo[id] = new() { BloqueadaAte = DateTimeOffset.UtcNow.AddMinutes(2), Endpoint = new() { Origem="endpoint", Rpm=30, Restante=97, Usado=3, Limite=100, Competencia="2026-10" } }; return true; });
            backup = await c.Store.BackupAsync(default);
            using var second = new IntegracaoArquivoRepository(Options.Create(c.Options), DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory, "keys"))));
            await Throws<IOException>(() => second.LerAsync(), "arquivo de lock impede dois processos/escritores do mesmo diretório");
            Check(File.Exists(Path.Combine(directory, "configuracao.json.bak")), "gravação atômica mantém cópia anterior");
        }
        using(var restarted = new Context(http, tls, directory: directory))
        {
            Check((await restarted.Store.LerAsync()).Consumo[id].Endpoint is { Rpm:30,Usado:3,Limite:100,Competencia:"2026-10" }, "descoberta de consumo é recuperada após reinício");
            Check((await restarted.Store.LerSegredoAsync(id, "cred")).Valor == "segredo-backup-local" && (await restarted.Store.LerAsync()).Consumo[id].BloqueadaAte > DateTimeOffset.UtcNow, "reinício preserva credencial criptografada e Retry-After");
            var stale = await restarted.Manager.ObterAsync(id, default);
            var newer = await restarted.Manager.ObterAsync(id, default); await restarted.Manager.SalvarAsync(newer, default);
            await Throws<IntegracaoException>(() => restarted.Manager.SalvarAsync(stale, default), "controle de revisão impede sobrescrever configuração concorrente");
            var original = await File.ReadAllTextAsync(Path.Combine(directory, "configuracao.json"));
            await File.WriteAllTextAsync(Path.Combine(directory, "configuracao.json"), "{");
            await Throws<IntegracaoException>(() => restarted.Store.LerAsync(), "arquivo inválido falha fechado, sem zerar configuração");
            Check(await File.ReadAllTextAsync(Path.Combine(directory, "configuracao.json")) == "{", "arquivo corrompido é preservado para recuperação");
            await File.WriteAllTextAsync(Path.Combine(directory, "configuracao.json"), original);
        }
        var restore = directory + "_restaurado"; System.IO.Directory.CreateDirectory(restore);
        using (var stream = new MemoryStream(backup)) using(var zip = new ZipArchive(stream)) zip.ExtractToDirectory(restore);
        // Como em produção, a restauração também necessita das chaves de Data Protection separadas.
        System.IO.Directory.CreateDirectory(Path.Combine(restore, "keys"));
        foreach(var key in System.IO.Directory.GetFiles(Path.Combine(directory, "keys"))) File.Copy(key, Path.Combine(restore, "keys", Path.GetFileName(key)));
        using(var restored = new Context(http, tls, directory: restore)) Check((await restored.Store.LerSegredoAsync(id, "cred")).Valor == "segredo-backup-local" && (await restored.Store.LerAsync()).Integracoes.Any(a => a.Id == id), "backup e restauração com chaves originais recuperam contrato e credencial");
        System.IO.Directory.Delete(directory, true); System.IO.Directory.Delete(restore, true);
    }
    public static async Task ExecutarSqlAsync(SqlLeadScopeB2B sql, int usuarioId)
    {
        await using var http = new ApiTestServer(); await using var tls = new ApiTestServer(true);
        var schema = (await sql.ConsultarAsync("SELECT COUNT(*) FROM sys.tables", r => r.GetInt32(0))).Single();
        var source = (await sql.ConsultarAsync("INSERT INTO dbo.FonteDados (Nome,Tipo) OUTPUT INSERTED.Id VALUES ('API privada teste','API')", r => r.GetInt32(0))).Single();
        var company = (await sql.ConsultarAsync("INSERT INTO dbo.Empresas (RazaoSocial,Cnpj,Status) OUTPUT INSERTED.Id VALUES ('Empresa original','00776574000156','VALIDADA')", r => r.GetInt32(0))).Single();
        var person = (await sql.ConsultarAsync("INSERT INTO dbo.Pessoas (Nome,Email) OUTPUT INSERTED.Id VALUES ('Pessoa original','api-pessoa@example.test')", r => r.GetInt32(0))).Single();
        var repo = new IntegracaoRepository(sql);
        using var c = new Context(http, tls, repo);
        var manifest = Manifest(http.Url);
        manifest.Operacoes[0].Mapeamentos.Add(new() { Caminho = "$.cnpj", Destino = "Empresa.Cnpj", Transformacao = "cnpj" });
        var api = await Ativar(c, manifest, source);
        http.Handler = _ => Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Empresa externa"},"cnpj":"00776574000156"}"""));
        var request = Request(api); request.UsuarioId = usuarioId; request.EmpresaId = company; request.PersistirEvidencias = true;
        var result = await c.Executor.ExecutarAsync(request, default);
        Check(result.Sucesso && result.Evidencias == 2, "SQL: motor genérico → HTTP local → DadosColetados e Logs nas tabelas existentes");
        Check((await c.Executor.ExecutarAsync(request, default)).Evidencias == 0, "SQL: evidências repetidas são idempotentes");
        var value = (await sql.ConsultarAsync("SELECT RazaoSocial FROM dbo.Empresas WHERE Id=@id", r => r.GetString(0), new SqlParameter("@id", company))).Single();
        Check(value == "Empresa original", "SQL: coleta não atualiza cadastro validado automaticamente");
        var evidence = (await repo.ListarAsync()).Single(d => d.EmpresaId == company && d.Campo == "RazaoSocial");
        await sql.ExecutarAsync("UPDATE dbo.Empresas SET Cnpj='19131243000197' WHERE Id=@id", new SqlParameter("@id", company));
        await Throws<ConflitoException>(() => repo.RevisarColetaAsync(evidence.Id, "Empresa original", true, usuarioId), "SQL: alteração de identidade impede aplicação da evidência");
        await sql.ExecutarAsync("UPDATE dbo.Empresas SET Cnpj='00776574000156' WHERE Id=@id", new SqlParameter("@id", company));
        await repo.RevisarColetaAsync(evidence.Id, "Empresa original", true, usuarioId);
        Check((await repo.ObterRevisaoColetaAsync(evidence.Id)) is { Revisada: true, ValorAtual: "Empresa externa" }, "SQL: aprovação humana aplica campo e audita decisão");
        await Throws<ConflitoException>(() => repo.RevisarColetaAsync(evidence.Id, "Empresa externa", true, usuarioId), "SQL: decisão duplicada recusada");
        Check(!(await repo.CandidatosIntegracaoAsync(source, false, default)).Any(d => d.EmpresaId == company), "SQL: automação não seleciona empresa já enriquecida pela fonte");
        Check((await repo.CandidatosIntegracaoAsync(source, false, default, somenteSemEvidencias: false)).Any(d => d.EmpresaId == company), "SQL: critério de registros ativos permite atualizar evidências existentes sem sobrescrever cadastro");
        var people = Manifest(http.Url, "/person"); people.Operacoes[0].Mapeamentos = [new() { Caminho = "$.company.name", Destino = "Pessoa.Nome", Obrigatorio = true }];
        var personApi = await Ativar(c, people, source); request = Request(personApi); request.UsuarioId = usuarioId; request.PessoaId = person; request.PersistirEvidencias = true;
        Check((await c.Executor.ExecutarAsync(request, default)).Evidencias == 1, "SQL: evidências genéricas de pessoas usam PessoaId sem empresa fictícia");
        var personEvidence = (await repo.ListarAsync()).Single(d => d.PessoaId == person && d.Campo == "Nome");
        await repo.RevisarColetaAsync(personEvidence.Id, "Pessoa original", true, usuarioId);
        Check((await repo.ObterRevisaoColetaAsync(personEvidence.Id)) is { Revisada: true, ValorAtual: "Empresa externa" }, "SQL: revisão de pessoa reutiliza normalização e auditoria existentes");
        request.PessoaId = 999999;
        Check((await c.Executor.ExecutarAsync(request, default)).Codigo == "DESTINO", "SQL: destino ausente não gera pessoa, empresa ou relacionamento");
        using(var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); await Throws<OperationCanceledException>(() => repo.CandidatosIntegracaoAsync(source, false, cancelled.Token), "SQL: cancelamento propagado"); }
        var fonteAuto=(await sql.ConsultarAsync("INSERT INTO dbo.FonteDados (Nome,Tipo) OUTPUT INSERTED.Id VALUES ('Automacao genérica','API')",r=>r.GetInt32(0))).Single();
        var automatico=Manifest(http.Url,"/enrich");
        automatico.Operacoes[0].Parametros=[new(){Nome="documento",Local="query",CampoOrigem="Empresa.Cnpj",Formato="cnpjNumerico",Obrigatorio=true}];
        automatico.Operacoes[0].Rotina=new(){Habilitada=false,UsuarioId=usuarioId,Criterio="empresasSemEvidencias",PersistirEvidencias=true,LimitePorExecucao=1};
        var apiAuto=await Ativar(c,automatico,fonteAuto); apiAuto.Contrato.Operacoes[0].Rotina.Habilitada=true; await c.Manager.SalvarAsync(apiAuto,default); c.Options.AutomacaoHabilitada=true;
        await c.Provider.GetRequiredService<AutomacaoIntegracaoService>().ExecutarPendentesAsync(default);
        var novas=(await sql.ConsultarAsync("SELECT COUNT(*) FROM dbo.DadosColetados WHERE FonteDadosId=@fonte",r=>r.GetInt32(0),new SqlParameter("@fonte",fonteAuto))).Single();
        Check(novas==1,"SQL: automação genérica transforma resposta em evidência para empresa existente");
        var chamadas=http.Calls;
        await c.Provider.GetRequiredService<AutomacaoIntegracaoService>().ExecutarPendentesAsync(default);
        Check(http.Calls==chamadas,"SQL: agendamento impede repetição do enriquecimento no mesmo ciclo");
        Check(schema == 14 && (await sql.ConsultarAsync("SELECT COUNT(*) FROM sys.tables", r => r.GetInt32(0))).Single() == 14, "SQL: esquema acadêmico permanece com as 14 tabelas");
    }
}
