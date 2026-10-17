using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LeadScopeB2B.Data;
using LeadScopeB2B.Integration;
using LeadScopeB2B.Models;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using LeadScopeB2B.ViewModels.Empresas;
using LeadScopeB2B.ViewModels.Leads;
using LeadScopeB2B.ViewModels.Pessoas;
using LeadScopeB2B.ViewModels.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
var settings = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true).AddUserSecrets<LeadScopeB2B.Data.SqlLeadScopeB2B>(true).AddEnvironmentVariables().Build();
var source = Environment.GetEnvironmentVariable("LEADSCOPE_TEST_CONNECTION") ?? settings.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("Configure LEADSCOPE_TEST_CONNECTION ou a conexão de desenvolvimento. Os testes criam e excluem seu próprio banco; não usam as tabelas da base de origem.");
var database = "LeadScopeB2B_Tests_" + Guid.NewGuid().ToString("N");
var csb = new SqlConnectionStringBuilder(source) { InitialCatalog = "master" };
await using var master = new SqlConnection(csb.ConnectionString);
await master.OpenAsync();
await new SqlCommand($"CREATE DATABASE [{database}]", master).ExecuteNonQueryAsync();
csb.InitialCatalog = database;
var testConnection = csb.ConnectionString;
Process? app = null;
var appLog = new StringBuilder();
var assertions = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FALHOU: " + name); assertions++; Console.WriteLine("OK: " + name); }
async Task Throws<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); } catch (T) { Check(true, name); return; }
    throw new Exception("FALHOU (não lançou " + typeof(T).Name + "): " + name);
}
try
{
    await using (var conn = new SqlConnection(testConnection))
    {
        await conn.OpenAsync();
        var schema = await File.ReadAllTextAsync(Path.Combine(root, "Docs/Database/EsquemaAtual.sql"));
        // Instalação anterior com dados: exercita a migração incremental antes do código novo.
        schema = schema.Replace("    [versao_sessao] bigint NOT NULL CONSTRAINT DF_Usuarios_VersaoSessao DEFAULT (0),\n", "");
        foreach (var batch in Regex.Split(schema, @"^GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await new SqlCommand(batch, conn).ExecuteNonQueryAsync();
        var hashMigracao = new PasswordHasher<Usuarios>().HashPassword(new Usuarios(), "Migracao#Teste123");
        await using var seed = new SqlCommand("INSERT INTO dbo.Usuarios (nome,email,senha_hash,perfil_acesso_id) VALUES (N'Usuário anterior','anterior@example.test',@hash,3)", conn);
        seed.Parameters.Add("@hash", System.Data.SqlDbType.NVarChar, 255).Value = hashMigracao;
        await seed.ExecuteNonQueryAsync();
        var migration = await File.ReadAllTextAsync(Path.Combine(root, "Docs/Database/Migrations/001_UsuariosVersaoSessao.sql"));
        await new SqlCommand(migration, conn).ExecuteNonQueryAsync();
        await new SqlCommand(migration, conn).ExecuteNonQueryAsync();
        await using var verify = new SqlCommand("SELECT senha_hash,versao_sessao,status,perfil_acesso_id FROM dbo.Usuarios WHERE email='anterior@example.test'", conn);
        await using var row = await verify.ExecuteReaderAsync();
        Check(await row.ReadAsync() && row.GetString(0) == hashMigracao && row.GetInt64(1) == 0 && row.GetString(2) == "ATIVO" && row.GetInt32(3) == 3, "migração incremental repetível preserva conta existente");
    }
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "ConnectionStrings:DefaultConnection", testConnection } }).Build();
    var sql = new SqlLeadScopeB2B(config);
    var pessoaRepo = new PessoaRepository(sql); var empresaRepo = new EmpresaRepository(sql); var usuarioRepo = new UsuarioRepository(sql);
    var leadRepo = new LeadRepository(sql, pessoaRepo, empresaRepo);
    var pessoas = new PessoaService(pessoaRepo); var empresas = new EmpresaService(empresaRepo); var leads = new LeadService(leadRepo);
    var usuarios = new UsuarioService(usuarioRepo, new PasswordHasher<Usuarios>());
    var fontes = new FonteDadosService(new FonteDadosRepository(sql)); var cnaes = new CnaeService(new CnaeRepository(sql));
    var integracao = new IntegracaoService(new IntegracaoRepository(sql), fontes);
    var exportacao = new ExportacaoService(new ExportacaoRepository(sql));
    const string senha = "Teste#Local123!";
    var bootstrapInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    bootstrapInfo.ArgumentList.Add(Path.Combine(root, "bin/Debug/net10.0/LeadScopeB2B.dll")); bootstrapInfo.ArgumentList.Add("--criar-admin");
    bootstrapInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    bootstrapInfo.Environment["ConnectionStrings__DefaultConnection"] = testConnection;
    bootstrapInfo.Environment["LEADSCOPE_ADMIN_NOME"] = "Administrador de teste";
    bootstrapInfo.Environment["LEADSCOPE_ADMIN_EMAIL"] = "admin@example.test";
    bootstrapInfo.Environment["LEADSCOPE_ADMIN_SENHA"] = senha;
    using (var bootstrap = Process.Start(bootstrapInfo)!)
    {
        var output = await bootstrap.StandardOutput.ReadToEndAsync(); var error = await bootstrap.StandardError.ReadToEndAsync(); await bootstrap.WaitForExitAsync();
        Check(bootstrap.ExitCode == 0 && output.Contains("Administrador inicial criado"), "comando local cria administrador inicial");
        if (bootstrap.ExitCode != 0) throw new Exception(error);
    }
    var admin = (await usuarioRepo.ObterPorEmailAsync("admin@example.test"))!;
    Check(admin.Senha != senha && new PasswordHasher<Usuarios>().VerifyHashedPassword(admin, admin.Senha, senha) != PasswordVerificationResult.Failed, "senha armazenada com hash");
    await Throws<ConflitoException>(() => usuarios.CriarAdministradorInicialAsync("Outro", "outro@example.test", senha), "cadastro inicial não pode ser repetido");
    await Throws<ConflitoException>(() => usuarios.ExcluirAsync(admin.Id), "último administrador ativo preservado");
    await usuarios.CadastrarAsync(new UsuarioCreateViewModel { Nome = "Leitura", Email = "leitura@example.test", Senha = senha, PerfilAcessoId = 3 });
    await usuarios.CadastrarAsync(new UsuarioCreateViewModel { Nome = "Gestor", Email = "gestor@example.test", Senha = senha, PerfilAcessoId = 2 });
    var leitor = (await usuarioRepo.ObterPorEmailAsync("leitura@example.test"))!;
    var gestor = (await usuarioRepo.ObterPorEmailAsync("gestor@example.test"))!;
    var gestorForm = (await usuarios.ObterParaEdicaoAsync(gestor.Id))!;
    gestorForm.Nome = "Gestor atualizado"; await usuarios.EditarAsync(gestorForm);
    Check((await usuarioRepo.ObterPorIdAsync(gestor.Id)) is { } atualizado && atualizado.Nome == "Gestor atualizado" && atualizado.Senha == gestor.Senha, "edição de usuário preserva hash sem nova senha");
    await Throws<ConflitoException>(() => usuarios.CadastrarAsync(new UsuarioCreateViewModel { Nome = "Duplicado", Email = " ADMIN@example.test ", Senha = senha, PerfilAcessoId = 1 }), "e-mail de usuário normalizado e duplicado");
    Check(CadastroRules.Cnpj("11.222.333/0001-81") == "11222333000181", "normalização e DV de CNPJ numérico");
    Check(CadastroRules.Cnpj("12.ABC.345/01DE-35") == "12ABC34501DE35", "DV de CNPJ alfanumérico");
    await Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => Task.Run(() => CadastroRules.Cnpj("11.222.333/0001-00")), "CNPJ inválido rejeitado");
    var empresa = await empresas.CadastrarAsync(new EmpresaFormViewModel { RazaoSocial = "Empresa Teste", Cnpj = "11.222.333/0001-81", Email = "contato@example.test", Estado = "sp", Status = "VALIDADA" });
    var eform = (await empresas.ObterParaEdicaoAsync(empresa))!; eform.Cidade = "São Paulo"; eform.UrlSite = "https://example.test"; await empresas.EditarAsync(eform);
    Check((await empresas.ObterDetalhesAsync(empresa))?.Cidade == "São Paulo", "empresa atualizada sem rastreamento EF");
    await Throws<ConflitoException>(() => empresas.CadastrarAsync(new EmpresaFormViewModel { RazaoSocial = "Duplicada", Cnpj = "11222333000181" }), "CNPJ duplicado em outra formatação");
    var semCnpj = await empresas.CadastrarAsync(new EmpresaFormViewModel { RazaoSocial = "Sem CNPJ 1" });
    await empresas.CadastrarAsync(new EmpresaFormViewModel { RazaoSocial = "Sem CNPJ 2" });
    Check((await empresas.ListarAsync()).Count == 3, "índice filtrado permite vários CNPJs nulos");
    var pessoa = await pessoas.CadastrarAsync(new PessoaFormViewModel { Nome = "=Contato de teste", Email = "pessoa@example.test", Status = "VALIDADO" });
    var pform = (await pessoas.ObterParaEdicaoAsync(pessoa))!; pform.Telefone = "11999999999"; await pessoas.EditarAsync(pform);
    Check((await pessoas.ObterDetalhesAsync(pessoa))?.Telefone == "11999999999", "pessoa atualizada por SQL explícito");
    await Throws<ConflitoException>(() => pessoas.CadastrarAsync(new PessoaFormViewModel { Nome = "Duplicada", Email = "pessoa@example.test" }), "pessoa duplicada por e-mail");
    var concorrentes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async i => { try { await pessoas.CadastrarAsync(new PessoaFormViewModel { Nome = "Concorrente " + i, Email = "concorrente@example.test" }); return true; } catch (ConflitoException) { return false; } }));
    Check(concorrentes.Count(x => x) == 1, "duplicidade concorrente protegida por transação");
    await cnaes.CadastrarAsync(new Cnaes { Numero = "6201-5/01", Descricao = "Desenvolvimento de software" });
    await cnaes.CadastrarAsync(new Cnaes { Numero = "6311900", Descricao = "Tratamento de dados" });
    await cnaes.AssociarAsync(empresa, "6201501", true); await cnaes.AssociarAsync(empresa, "6311900", true);
    Check((await cnaes.ListarDaEmpresaAsync(empresa)).Count(c => c.Principal) == 1, "apenas um CNAE principal por empresa");
    Check((await empresas.ListarAsync(cnae: "6201-5/01")).Count == 1, "filtro de empresa por CNAE formatado");
    await Throws<SqlException>(() => cnaes.AssociarAsync(empresa, "9999999", true), "CNAE inexistente rejeitado pelo relacionamento");
    Check((await cnaes.ListarDaEmpresaAsync(empresa)).Single(c => c.Principal).CnaeNumero == "6311900", "falha de associação preserva o principal anterior");
    await leads.CadastrarAsync(new LeadFormViewModel { PessoaId = pessoa, EmpresaId = empresa, Cargo = "Analista", StatusVinculo = "ATIVO" });
    await Throws<ConflitoException>(() => leads.CadastrarAsync(new LeadFormViewModel { PessoaId = pessoa, EmpresaId = empresa }), "lead duplicado por chave composta");
    await leads.RevisarAsync(pessoa, empresa, admin.Id, "QUALIFICADO", "Validado por teste");
    Check((await leads.ObterDetalhesAsync(pessoa, empresa)) is { StatusLead: "QUALIFICADO", Revisoes.Count: 1 }, "revisão atualiza lead e grava histórico");
    var lform = (await leads.ObterParaEdicaoAsync(pessoa, empresa))!; lform.Cargo = "Diretor"; await leads.EditarAsync(pessoa, empresa, lform);
    Check((await leads.ObterDetalhesAsync(pessoa, empresa)) is { StatusLead: "EM_ANALISE", DataRevisao: null }, "edição reabre revisão anterior");
    await Throws<ConflitoException>(() => exportacao.ExportarAsync([$"{pessoa}:{empresa}"], admin.Id), "exportação de lead não qualificado rejeitada");
    await leads.RevisarAsync(pessoa, empresa, admin.Id, "QUALIFICADO", null);
    var csv = Encoding.UTF8.GetString(await exportacao.ExportarAsync([$"{pessoa}:{empresa}"], admin.Id));
    Check(csv.Contains("'=Contato") && csv.Contains("Diretor"), "CSV com dados reais e proteção contra fórmula");
    await fontes.CadastrarAsync(new FonteDados { Nome = "Fonte de teste", Tipo = "API", Url = "https://example.test", Confiabilidade = 90 });
    var fonte = (await fontes.ListarAsync()).Single();
    var dado = new DadoRecebido { PessoaId = pessoa, Campo = "email", Valor = "alterado@example.test", NivelConfianca = 99 };
    var evidencia = await integracao.ReceberAsync(dado, fonte.Id, admin.Id);
    Check((await pessoas.ObterDetalhesAsync(pessoa))?.Email == "pessoa@example.test", "recebimento externo não altera cadastro antes da revisão");
    Check(await integracao.ReceberAsync(dado, fonte.Id, admin.Id) == evidencia, "reenvio pendente não duplica evidência");
    await integracao.RevisarAsync(evidencia, true, admin.Id);
    Check((await pessoas.ObterDetalhesAsync(pessoa))?.Email == "alterado@example.test", "revisão aplica dado validado");
    await Throws<ConflitoException>(() => integracao.RevisarAsync(evidencia, true, admin.Id), "evidência não pode ser aplicada duas vezes");
    var duplicada = await integracao.ReceberAsync(new DadoRecebido { PessoaId = pessoa, Campo = "email", Valor = "concorrente@example.test" }, fonte.Id, admin.Id);
    await Throws<ConflitoException>(() => integracao.RevisarAsync(duplicada, true, admin.Id), "aprovação que duplicaria pessoa é revertida");
    Check((await integracao.ObterDaFonteAsync(duplicada, fonte.Id))?.StatusValidacao == "PENDENTE", "rollback preserva evidência pendente");
    await integracao.RevisarAsync(duplicada, false, admin.Id);
    await pessoas.AlterarBloqueioAsync(pessoa, true);
    await Throws<ConflitoException>(() => exportacao.ExportarAsync([$"{pessoa}:{empresa}"], admin.Id), "bloqueio impede exportação");
    await Throws<ConflitoException>(() => integracao.ReceberAsync(dado, fonte.Id, admin.Id), "bloqueio impede enriquecimento");
    await pessoas.AlterarBloqueioAsync(pessoa, false);
    pform = (await pessoas.ObterParaEdicaoAsync(pessoa))!; pform.Status = "VALIDADO"; await pessoas.EditarAsync(pform);
    Check((await pessoas.ListarAsync("Contato")).Count == 1 && (await empresas.ListarAsync("Empresa Teste")).Count == 1, "filtros SQL de pesquisa");
    Check((await pessoas.ListarAsync("' OR 1=1 --")).Count == 0, "busca parametrizada resiste a injeção SQL");

    // Exercita o pipeline real: autenticação, cookies, CSRF, controllers, Razor, JSON e SQL Server.
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var readKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var info = new ProcessStartInfo("dotnet", Path.Combine(root, "bin/Debug/net10.0/LeadScopeB2B.dll")) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; info.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    info.Environment["ConnectionStrings__DefaultConnection"] = testConnection;
    foreach (var (k, u, send, i) in new[] { (key, admin.Id, true, 0), (readKey, leitor.Id, false, 1) })
    {
        info.Environment[$"Integracoes__Chaves__{i}__Sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(k)));
        info.Environment[$"Integracoes__Chaves__{i}__UsuarioId"] = u.ToString(); info.Environment[$"Integracoes__Chaves__{i}__FonteDadosId"] = fonte.Id.ToString(); info.Environment[$"Integracoes__Chaves__{i}__PermiteEnvio"] = send.ToString();
    }
    app = Process.Start(info)!;
    app.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (appLog) appLog.AppendLine(e.Data); }; app.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (appLog) appLog.AppendLine(e.Data); }; app.BeginOutputReadLine(); app.BeginErrorReadLine();
    HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };
    using var web = Client();
    for (var i = 0; i < 100; i++) { try { using var ready = await web.GetAsync("/"); break; } catch (HttpRequestException) { await Task.Delay(100); } }
    static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    async Task Login(HttpClient client, string email)
    {
        var html = await client.GetStringAsync("/Conta/Login");
        var response = await client.PostAsync("/Conta/Login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", email }, { "Senha", senha }, { "__RequestVerificationToken", Token(html) } }));
        Check(response.StatusCode == HttpStatusCode.Redirect, "login MVC: " + email);
    }
    foreach (var asset in new[] { "/lib/bootstrap/dist/css/bootstrap.min.css", "/lib/jquery/dist/jquery.min.js", "/lib/jquery-validation/dist/jquery.validate.min.js", "/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js", "/css/site.css", "/LeadScopeB2B.styles.css" })
        Check((await web.GetAsync(asset)).StatusCode == HttpStatusCode.OK, "recurso da interface: " + asset);
    Check((await web.GetAsync("/Empresas")).StatusCode == HttpStatusCode.Redirect, "interface exige login");
    var unauthenticated = await web.GetAsync("/api/empresas");
    Check(unauthenticated.StatusCode == HttpStatusCode.Unauthorized && unauthenticated.Content.Headers.ContentType?.MediaType == "application/problem+json", "API sem chave retorna 401 em JSON");
    await Login(web, "admin@example.test");
    foreach (var path in new[] { "/Empresas", "/Empresas/Create", $"/Empresas/Edit/{empresa}", $"/Empresas/Details/{empresa}", "/Pessoas", "/Pessoas/Create", $"/Pessoas/Edit/{pessoa}", $"/Pessoas/Details/{pessoa}", "/Leads", "/Leads/Create", $"/Leads/Edit?pessoaId={pessoa}&empresaId={empresa}", $"/Leads/Details?pessoaId={pessoa}&empresaId={empresa}", "/Usuarios", "/Usuarios/Create", $"/Usuarios/Edit/{admin.Id}", "/Cnaes", "/Fontes", "/Integracoes", "/Auditoria" })
        Check((await web.GetAsync(path)).StatusCode == HttpStatusCode.OK, "página MVC: " + path);
    var editHtml = await web.GetStringAsync($"/Pessoas/Edit/{pessoa}");
    var editPerson = await web.PostAsync($"/Pessoas/Edit/{pessoa}", new FormUrlEncodedContent(new Dictionary<string, string> { { "Id", pessoa.ToString() }, { "Nome", "=Contato de teste" }, { "Email", "alterado@example.test" }, { "Telefone", "1144445555" }, { "Status", "VALIDADO" }, { "__RequestVerificationToken", Token(editHtml) } }));
    Check(editPerson.StatusCode == HttpStatusCode.Redirect && (await pessoas.ObterDetalhesAsync(pessoa))?.Telefone == "1144445555", "formulário MVC atualiza pessoa");
    var forbiddenLeadHtml = await web.GetStringAsync($"/Leads/Edit?pessoaId={pessoa}&empresaId={empresa}");
    var forbiddenLead = await web.PostAsync($"/Leads/Edit?pessoaId={pessoa}&empresaId={empresa}", new FormUrlEncodedContent(new Dictionary<string, string> { { "PessoaId", pessoa.ToString() }, { "EmpresaId", empresa.ToString() }, { "StatusLead", "QUALIFICADO" }, { "StatusVinculo", "ATIVO" }, { "__RequestVerificationToken", Token(forbiddenLeadHtml) } }));
    Check(forbiddenLead.StatusCode == HttpStatusCode.OK && (await forbiddenLead.Content.ReadAsStringAsync()).Contains("Estado de cadastro"), "edição MVC não contorna revisão de qualificação");
    Check((await web.PostAsync("/Empresas/Create", new FormUrlEncodedContent(new Dictionary<string, string> { { "RazaoSocial", "Sem token" } }))).StatusCode == HttpStatusCode.BadRequest, "formulário sem CSRF rejeitado");
    var createHtml = await web.GetStringAsync("/Empresas/Create");
    var create = await web.PostAsync("/Empresas/Create", new FormUrlEncodedContent(new Dictionary<string, string> { { "RazaoSocial", "Empresa via formulário" }, { "Status", "PENDENTE" }, { "__RequestVerificationToken", Token(createHtml) } }));
    Check(create.StatusCode == HttpStatusCode.Redirect && (await empresas.ListarAsync("Empresa via formulário")).Count == 1, "formulário MVC cadastra empresa no SQL Server");
    var personHtml = await web.GetStringAsync("/Pessoas/Create");
    var dupResponse = await web.PostAsync("/Pessoas/Create", new FormUrlEncodedContent(new Dictionary<string, string> { { "Nome", "Duplicada" }, { "Email", "alterado@example.test" }, { "Status", "PENDENTE" }, { "__RequestVerificationToken", Token(personHtml) } }));
    Check(dupResponse.StatusCode == HttpStatusCode.OK && (await dupResponse.Content.ReadAsStringAsync()).Contains("existe uma pessoa"), "erro de duplicidade retorna ao formulário MVC");
    using var readWeb = Client(); await Login(readWeb, "leitura@example.test");
    Check((await readWeb.GetAsync("/Empresas")).StatusCode == HttpStatusCode.OK, "leitura acessa consultas");
    Check((await readWeb.GetAsync("/Empresas/Create")).StatusCode == HttpStatusCode.Redirect && (await readWeb.GetAsync("/Conta/Negado")).StatusCode == HttpStatusCode.Forbidden, "leitura não acessa cadastro");
    Check((await readWeb.GetAsync("/Usuarios")).StatusCode == HttpStatusCode.Redirect, "leitura não administra usuários");
    Check((await web.GetAsync("/api/empresas")).StatusCode == HttpStatusCode.Unauthorized, "cookie MVC não autentica API externa");
    using var api = Client(); api.DefaultRequestHeaders.Add("X-Api-Key", key);
    Check((await api.GetAsync("/api/empresas")).StatusCode == HttpStatusCode.OK, "API consulta JSON com chave válida");
    Check((await api.GetAsync("/api/pessoas/999999")).StatusCode == HttpStatusCode.NotFound, "API retorna 404 para destino inexistente");
    using var invalidApi = Client(); invalidApi.DefaultRequestHeaders.Add("X-Api-Key", "invalida");
    Check((await invalidApi.GetAsync("/api/empresas")).StatusCode == HttpStatusCode.Unauthorized, "API recusa chave incorreta");
    var wrongContent = await api.PostAsync("/api/integracoes/dados", new StringContent("{}"));
    Check(wrongContent.StatusCode == HttpStatusCode.UnsupportedMediaType, "API exige JSON no envio");
    var malformed = await api.PostAsJsonAsync("/api/integracoes/dados", new { pessoaId = pessoa, empresaId = empresa, campo = "email", valor = "a@example.test" });
    Check(malformed.StatusCode == HttpStatusCode.BadRequest, "API valida destino exclusivo");
    Check((await api.PostAsJsonAsync("/api/integracoes/dados", new { pessoaId = pessoa, campo = "senha_hash", valor = "x" })).StatusCode == HttpStatusCode.BadRequest, "API rejeita campo fora da lista permitida");
    var received = await api.PostAsJsonAsync("/api/integracoes/dados", new { empresaId = empresa, campo = "telefone", valor = "1133334444" });
    Check(received.StatusCode == HttpStatusCode.Accepted && received.Headers.Location is not null, "API recebe evidência com 202 e Location");
    var body = JsonDocument.Parse(await received.Content.ReadAsStringAsync()); var externalId = body.RootElement.GetProperty("id").GetInt32();
    Check((await api.GetAsync(received.Headers.Location)).StatusCode == HttpStatusCode.OK, "Location da evidência pode ser consultado");
    Check((await empresas.ObterDetalhesAsync(empresa))?.Telefone is null, "evidência HTTP aguarda revisão humana");
    var reviewHtml = await web.GetStringAsync("/Integracoes");
    var reviewed = await web.PostAsync("/Integracoes/Revisar", new FormUrlEncodedContent(new Dictionary<string, string> { { "id", externalId.ToString() }, { "aprovar", "true" }, { "__RequestVerificationToken", Token(reviewHtml) } }));
    Check(reviewed.StatusCode == HttpStatusCode.Redirect && (await empresas.ObterDetalhesAsync(empresa))?.Telefone == "1133334444", "interface revisa e aplica evidência recebida via API");
    using var readApi = Client(); readApi.DefaultRequestHeaders.Add("X-Api-Key", readKey);
    Check((await readApi.GetAsync("/api/cnaes")).StatusCode == HttpStatusCode.OK, "chave de leitura consulta API");
    Check((await readApi.PostAsJsonAsync("/api/integracoes/dados", new { pessoaId = pessoa, campo = "email", valor = "x@example.test" })).StatusCode == HttpStatusCode.Forbidden, "chave de leitura não envia dados");
    var apiLead = await api.PostAsJsonAsync("/api/integracoes/leads", new { pessoaId = pessoa, empresaId = semCnpj, cargo = "Consultor" });
    Check(apiLead.StatusCode == HttpStatusCode.Created && (await leads.ObterDetalhesAsync(pessoa, semCnpj)) is { StatusLead: "NOVO", StatusVinculo: "INCERTO" }, "lead externo permanece novo e incerto");
    Check((await api.PostAsJsonAsync("/api/integracoes/leads", new { pessoaId = pessoa, empresaId = semCnpj })).StatusCode == HttpStatusCode.Conflict, "API retorna 409 para lead duplicado");
    await fontes.CadastrarAsync(new FonteDados { Nome = "Outra fonte", Tipo = "API" });
    Check(await integracao.ObterDaFonteAsync(externalId, (await fontes.ListarAsync()).Single(f => f.Nome == "Outra fonte").Id) is null, "evidência de outra fonte não é exposta");
    using var gestorWeb = Client(); await Login(gestorWeb, "gestor@example.test");
    Check((await gestorWeb.GetAsync("/Empresas/Create")).StatusCode == HttpStatusCode.OK && (await gestorWeb.GetAsync("/Usuarios")).StatusCode == HttpStatusCode.Redirect, "perfil de operação cadastra sem administrar usuários");
    gestorForm = (await usuarios.ObterParaEdicaoAsync(gestor.Id))!; gestorForm.PerfilAcessoId = 3; await usuarios.EditarAsync(gestorForm);
    Check((await gestorWeb.GetAsync("/Empresas/Create")).StatusCode == HttpStatusCode.Redirect, "mudança de perfil revoga permissão na sessão existente");
    gestorForm.NovaSenha = "Nova#Senha123!"; await usuarios.EditarAsync(gestorForm);
    Check((await gestorWeb.GetAsync("/Empresas")).StatusCode == HttpStatusCode.Redirect, "troca de senha invalida cookie existente");
    var conta = new ContaService(usuarioRepo, new PasswordHasher<Usuarios>());
    Check(await conta.AutenticarAsync("gestor@example.test", senha) is null && await conta.AutenticarAsync("gestor@example.test", "Nova#Senha123!") is not null, "troca de senha substitui hash e credencial anterior");
    await fontes.AlterarAtivoAsync(fonte.Id, false);
    Check((await api.PostAsJsonAsync("/api/integracoes/dados", new { pessoaId = pessoa, campo = "telefone", valor = "123" })).StatusCode == HttpStatusCode.Conflict, "fonte desativada impede envio");
    await usuarios.ExcluirAsync(leitor.Id);
    Check((await readWeb.GetAsync("/Empresas")).StatusCode == HttpStatusCode.Redirect && (await readApi.GetAsync("/api/empresas")).StatusCode == HttpStatusCode.Unauthorized, "desativação revoga cookie e chave");
    await leads.ExcluirAsync(pessoa, semCnpj); Check(await leads.ObterDetalhesAsync(pessoa, semCnpj) is null, "remoção lógica de lead");
    await empresas.ExcluirAsync(semCnpj); Check(await empresas.ObterDetalhesAsync(semCnpj) is null, "remoção lógica de empresa");
    await pessoas.ExcluirAsync(pessoa); Check(await pessoas.ObterDetalhesAsync(pessoa) is null && (await leads.ListarAsync()).Count == 0, "remoção lógica de pessoa oculta seus leads");
    Check((await new AuditoriaService(new AuditoriaRepository(sql)).ListarAsync()).Count >= 8, "auditoria registra login, recebimento, revisão e exportação");
    await ContaFlowTests.ExecutarAsync(root, testConnection, Check);
    Console.WriteLine($"SUCESSO: {assertions} verificações com SQL Server e HTTP reais.");
}
catch
{
    lock (appLog) if (appLog.Length > 0) Console.Error.WriteLine(appLog.ToString());
    throw;
}
finally
{
    if (app is not null && !app.HasExited) { app.Kill(true); await app.WaitForExitAsync(); }
    app?.Dispose();
    SqlConnection.ClearAllPools();
    await new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]", master).ExecuteNonQueryAsync();
    Console.WriteLine("Banco temporário excluído; base de origem preservada.");
}
