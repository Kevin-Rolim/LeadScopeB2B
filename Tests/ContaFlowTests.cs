using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using LeadScopeB2B.ViewModels.Usuarios;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

internal static class ContaFlowTests
{
    private const string Senha = "Cadastro#Local123!";
    private const string SenhaNova = "Alterada#Local123!";
    private const string Email = "cadastro-http@example.test";
    private const string EmailNovo = "perfil-http@example.test";
    private static string Texto(string html) => WebUtility.HtmlDecode(html);
    private static string Token(string html) => Texto(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> values, string? formPath = null)
    {
        values["__RequestVerificationToken"] = Token(await client.GetStringAsync(formPath ?? path));
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string senha) => PostAsync(client, "/Conta/Login", new() { { "Email", email }, { "Senha", senha } });

    public static async Task ExecutarAsync(string root, string connection, Action<bool, string> check)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "ConnectionStrings:DefaultConnection", connection } }).Build();
        var repo = new UsuarioRepository(new SqlLeadScopeB2B(config));
        var hasher = new PasswordHasher<Usuarios>();
        var usuarios = new UsuarioService(repo, hasher);
        var admin = (await repo.ObterPorEmailAsync("admin@example.test"))!;
        await using (var app = await AplicacaoTeste.IniciarAsync(root, connection))
        {
            using var anon = app.Client();
            var loginPage = await anon.GetStringAsync("/Conta/Login");
            check(loginPage.Contains("/Conta/Cadastro") && loginPage.Contains("/Conta/RecuperarAcesso"), "login oferece cadastro e recuperação");
            var registerPage = await anon.GetStringAsync("/Conta/Cadastro");
            check(registerPage.Contains("ConfirmacaoSenha") && !registerPage.Contains("name=\"PerfilAcessoId\""), "cadastro público tem confirmação e não oferece escolha de perfil");
            var noCsrf = await anon.PostAsync("/Conta/Cadastro", new FormUrlEncodedContent(new Dictionary<string, string> { { "Nome", "Sem token" } }));
            check(noCsrf.StatusCode == HttpStatusCode.BadRequest, "cadastro público exige CSRF");
            var mismatch = await PostAsync(anon, "/Conta/Cadastro", new() { { "Nome", "Novo" }, { "Email", Email }, { "Senha", Senha }, { "ConfirmacaoSenha", "diferente" } });
            var mismatchHtml = Texto(await mismatch.Content.ReadAsStringAsync());
            check(mismatch.StatusCode == HttpStatusCode.OK && mismatchHtml.Contains("não coincidem") && await repo.ObterPorEmailAsync(Email) is null, "confirmação inválida não cadastra usuário");
            check(!mismatchHtml.Contains("value=\"" + Senha + "\""), "formulário não devolve a senha preenchida");
            var invalid = await PostAsync(anon, "/Conta/Cadastro", new() { { "Nome", " " }, { "Email", "invalido" }, { "Senha", "123" }, { "ConfirmacaoSenha", "123" } });
            check(invalid.StatusCode == HttpStatusCode.OK && Texto(await invalid.Content.ReadAsStringAsync()).Contains("e-mail válido"), "cadastro valida nome, e-mail e tamanho da senha no servidor");
            var created = await PostAsync(anon, "/Conta/Cadastro", new() { { "Nome", "Usuário público" }, { "Email", Email.ToUpperInvariant() }, { "Senha", Senha }, { "ConfirmacaoSenha", Senha }, { "PerfilAcessoId", "1" }, { "Status", "BLOQUEADO" }, { "Id", admin.Id.ToString() } });
            var usuario = (await repo.ObterPorEmailAsync(Email))!;
            check(created.StatusCode == HttpStatusCode.Redirect && created.Headers.Location?.AbsolutePathOrOriginal() == "/Conta/Login", "cadastro pela interface direciona para login");
            check(usuario is { Status: "ATIVO" } && Acesso.Papel(usuario) == "LEITURA", "campos forjados não promovem cadastro público a administrador");
            check(usuario.Senha != Senha && hasher.VerifyHashedPassword(usuario, usuario.Senha, Senha) != PasswordVerificationResult.Failed, "cadastro HTTP armazena hash seguro no SQL Server");
            check(Texto(await anon.GetStringAsync("/Conta/Login")).Contains("Cadastro realizado"), "cadastro exibe confirmação adequada");
            var duplicate = await PostAsync(anon, "/Conta/Cadastro", new() { { "Nome", "Duplicado" }, { "Email", Email }, { "Senha", Senha }, { "ConfirmacaoSenha", Senha } });
            check(duplicate.StatusCode == HttpStatusCode.OK && Texto(await duplicate.Content.ReadAsStringAsync()).Contains("Não foi possível criar a conta") && (await repo.ListarAsync()).Count(u => u.Email == Email) == 1, "cadastro duplicado é recusado sem duplicar a linha");
            var registrationLimit = await PostAsync(anon, "/Conta/Cadastro", new() { { "Nome", "Limite" }, { "Email", "limite@example.test" }, { "Senha", Senha }, { "ConfirmacaoSenha", Senha } });
            check(registrationLimit.StatusCode == HttpStatusCode.TooManyRequests, "cadastro aplica limite de tentativas");

            using var titular = app.Client();
            using var observador = app.Client();
            using var adminWeb = app.Client();
            var signedIn = await LoginAsync(titular, Email, Senha);
            check(signedIn.StatusCode == HttpStatusCode.Redirect && signedIn.Headers.Location?.OriginalString == "/Empresas", "usuário cadastrado entra com suas credenciais e vai ao catálogo");
            check((await LoginAsync(observador, Email, Senha)).StatusCode == HttpStatusCode.Redirect, "segunda sessão da conta criada");
            var adminLogin = await LoginAsync(adminWeb, "admin@example.test", "Teste#Local123!");
            check(adminLogin.StatusCode == HttpStatusCode.Redirect && adminLogin.Headers.Location?.OriginalString == "/Usuarios", "administrador entra na área administrativa");
            check((await titular.GetAsync("/Usuarios")).StatusCode == HttpStatusCode.Redirect && (await titular.GetAsync("/Fontes")).StatusCode == HttpStatusCode.Redirect, "usuário público não acessa áreas administrativas");
            var forgedAdmin = await PostAsync(titular, $"/Usuarios/Edit/{admin.Id}", new() { { "Id", admin.Id.ToString() }, { "Nome", "Invadido" }, { "PerfilAcessoId", "1" } }, "/Conta/Perfil");
            check(forgedAdmin.StatusCode == HttpStatusCode.Redirect && (await repo.ObterPorIdAsync(admin.Id))!.Nome == admin.Nome, "servidor bloqueia POST administrativo forjado");
            check((await titular.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.OK && (await titular.GetAsync("/Conta/AlterarSenha")).StatusCode == HttpStatusCode.OK, "titular acessa perfil e alteração de senha");
            var noProfileCsrf = await titular.PostAsync("/Conta/Perfil", new FormUrlEncodedContent(new Dictionary<string, string> { { "Nome", "Sem token" } }));
            check(noProfileCsrf.StatusCode == HttpStatusCode.BadRequest, "atualização de perfil exige CSRF");
            var wrongPassword = await PostAsync(titular, "/Conta/Perfil", new() { { "Nome", "Nome novo" }, { "Email", EmailNovo }, { "SenhaAtual", "incorreta" } });
            check(wrongPassword.StatusCode == HttpStatusCode.OK && Texto(await wrongPassword.Content.ReadAsStringAsync()).Contains("Senha atual incorreta") && (await repo.ObterPorIdAsync(usuario.Id))!.Email == Email, "perfil exige senha atual e preserva dados na falha");
            var duplicateProfile = await PostAsync(titular, "/Conta/Perfil", new() { { "Nome", "Nome novo" }, { "Email", admin.Email }, { "SenhaAtual", Senha } });
            check(duplicateProfile.StatusCode == HttpStatusCode.OK && (await repo.ObterPorIdAsync(usuario.Id))!.Email == Email, "perfil impede e-mail duplicado");
            var profile = await PostAsync(titular, "/Conta/Perfil", new() { { "Nome", "Titular atualizado" }, { "Email", EmailNovo }, { "SenhaAtual", Senha }, { "Id", admin.Id.ToString() }, { "PerfilAcessoId", "1" }, { "Status", "ATIVO" } });
            usuario = (await repo.ObterPorIdAsync(usuario.Id))!;
            check(profile.StatusCode == HttpStatusCode.Redirect && usuario.Nome == "Titular atualizado" && usuario.Email == EmailNovo && Acesso.Papel(usuario) == "LEITURA" && (await repo.ObterPorIdAsync(admin.Id))!.Nome == admin.Nome, "perfil atualiza apenas o titular sem permitir troca de permissões");
            check((await titular.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.OK && (await observador.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect, "perfil renova sessão atual e revoga a outra");
            check((await LoginAsync(observador, EmailNovo, Senha)).StatusCode == HttpStatusCode.Redirect, "e-mail atualizado permite novo login");
            var badChange = await PostAsync(titular, "/Conta/AlterarSenha", new() { { "SenhaAtual", "errada" }, { "NovaSenha", SenhaNova }, { "ConfirmacaoSenha", SenhaNova } });
            check(badChange.StatusCode == HttpStatusCode.OK && Texto(await badChange.Content.ReadAsStringAsync()).Contains("Senha atual incorreta"), "alteração de senha recusa senha atual incorreta");
            var changed = await PostAsync(titular, "/Conta/AlterarSenha", new() { { "SenhaAtual", Senha }, { "NovaSenha", SenhaNova }, { "ConfirmacaoSenha", SenhaNova } });
            check(changed.StatusCode == HttpStatusCode.Redirect && (await titular.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect && (await observador.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect, "troca de senha encerra todas as sessões");
            usuario = (await repo.ObterPorIdAsync(usuario.Id))!;
            check(hasher.VerifyHashedPassword(usuario, usuario.Senha, Senha) == PasswordVerificationResult.Failed && hasher.VerifyHashedPassword(usuario, usuario.Senha, SenhaNova) != PasswordVerificationResult.Failed, "nova senha substitui a anterior no banco");
            check((await LoginAsync(titular, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.Redirect, "login funciona após troca de senha");

            using var cookieAntesBloqueio = app.Client();
            check((await LoginAsync(cookieAntesBloqueio, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.Redirect, "sessão anterior ao bloqueio capturada");
            var blocked = await PostAsync(adminWeb, "/Usuarios/AlterarBloqueio", new() { { "id", usuario.Id.ToString() }, { "bloquear", "true" } }, $"/Usuarios/Details/{usuario.Id}");
            check(blocked.StatusCode == HttpStatusCode.Redirect && (await repo.ObterPorIdAsync(usuario.Id))!.Status == "BLOQUEADO", "administrador bloqueia conta pela interface");
            using var blockedLogin = app.Client();
            var blockedResponse = await LoginAsync(blockedLogin, EmailNovo, SenhaNova);
            check(blockedResponse.StatusCode == HttpStatusCode.OK && Texto(await blockedResponse.Content.ReadAsStringAsync()).Contains("E-mail ou senha inválidos, ou conta indisponível"), "conta bloqueada não entra e recebe erro genérico");
            check((await titular.GetAsync("/Empresas")).StatusCode == HttpStatusCode.Redirect, "bloqueio revoga sessão existente");
            var unblocked = await PostAsync(adminWeb, "/Usuarios/AlterarBloqueio", new() { { "id", usuario.Id.ToString() }, { "bloquear", "false" } }, $"/Usuarios/Details/{usuario.Id}");
            check(unblocked.StatusCode == HttpStatusCode.Redirect && (await repo.ObterPorIdAsync(usuario.Id))!.Status == "ATIVO", "administrador desbloqueia conta pela interface");
            check((await cookieAntesBloqueio.GetAsync("/Empresas")).StatusCode == HttpStatusCode.Redirect, "desbloqueio não revalida cookie que nunca foi usado durante o bloqueio");
            using var reenabled = app.Client();
            check((await LoginAsync(reenabled, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.Redirect, "conta desbloqueada pode entrar novamente");
            check((await reenabled.PostAsync("/Conta/Logout", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "logout exige CSRF");
            var logout = await PostAsync(reenabled, "/Conta/Logout", new(), "/Conta/Perfil");
            check(logout.StatusCode == HttpStatusCode.Redirect && (await reenabled.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect, "logout encerra acesso da sessão");

            var recoveryPage = await anon.GetStringAsync("/Conta/RecuperarAcesso");
            check(Texto(recoveryPage).Contains("recuperação por e-mail está indisponível") && !recoveryPage.Contains("Solicitar recuperação</button>"), "SMTP ausente é mostrado como indisponível sem simular envio");
            var csrf = Token(await anon.GetStringAsync("/Conta/Login"));
            var unavailable = await anon.PostAsync("/Conta/RecuperarAcesso", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", EmailNovo }, { "__RequestVerificationToken", csrf } }));
            var unknownUnavailable = await anon.PostAsync("/Conta/RecuperarAcesso", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", "ausente@example.test" }, { "__RequestVerificationToken", csrf } }));
            check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable && unknownUnavailable.StatusCode == unavailable.StatusCode && Texto(await unavailable.Content.ReadAsStringAsync()).Contains("indisponível"), "recuperação não configurada falha igualmente para qualquer e-mail");
            using var knownWrong = app.Client(); using var unknownWrong = app.Client();
            var known = await LoginAsync(knownWrong, EmailNovo, "errada"); var unknown = await LoginAsync(unknownWrong, "ausente@example.test", "errada");
            check(known.StatusCode == HttpStatusCode.OK && unknown.StatusCode == HttpStatusCode.OK && Texto(await known.Content.ReadAsStringAsync()).Contains("E-mail ou senha inválidos, ou conta indisponível") && Texto(await unknown.Content.ReadAsStringAsync()).Contains("E-mail ou senha inválidos, ou conta indisponível"), "login não revela existência de e-mail");
            var limited = await LoginAsync(unknownWrong, "ausente@example.test", "errada");
            check(limited.StatusCode == HttpStatusCode.TooManyRequests && Texto(await limited.Content.ReadAsStringAsync()).Contains("Muitas tentativas"), "login aplica limite com mensagem adequada");
        }

        await using var smtp = new SmtpTestServer();
        await using var configured = await AplicacaoTeste.IniciarAsync(root, connection, smtp.Porta);
        using var recovery = configured.Client();
        check((await recovery.GetStringAsync("/Conta/RecuperarAcesso")).Contains("Solicitar recuperação</button>"), "recuperação configurada disponibiliza formulário");
        var reqUnknown = await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", "ausente@example.test" } });
        var reqKnown = await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", EmailNovo } });
        check(reqKnown.StatusCode == HttpStatusCode.Redirect && reqKnown.Headers.Location?.OriginalString == reqUnknown.Headers.Location?.OriginalString, "recuperação responde igualmente a conta existente e ausente");
        var message = await smtp.ReceberAsync();
        var link = ExtrairLink(message);
        check(message.Contains(EmailNovo) && link.StartsWith(configured.BaseUrl + "/Conta/RedefinirSenha?token="), "SMTP recebe mensagem real com origem configurada e token protegido");
        var token = Uri.UnescapeDataString(new Uri(link).Query["?token=".Length..]);
        var resetPath = "/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(token);
        var resetPage = await recovery.GetAsync(resetPath);
        check(resetPage.StatusCode == HttpStatusCode.OK && (await resetPage.Content.ReadAsStringAsync()).Contains("Salvar nova senha") && resetPage.Headers.CacheControl?.NoStore == true && resetPage.Headers.GetValues("Referrer-Policy").Contains("no-referrer"), "link abre formulário sem cache nem envio de referência");
        using var beforeReset = configured.Client(); using var otherReset = configured.Client();
        check((await LoginAsync(beforeReset, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.Redirect && (await LoginAsync(otherReset, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.Redirect, "sessões reais antes da recuperação");
        var invalidLink = await recovery.GetStringAsync("/Conta/RedefinirSenha?token=invalido");
        check(Texto(invalidLink).Contains("link é inválido ou expirou") && !invalidLink.Contains("Salvar nova senha"), "link inválido não oferece redefinição");
        var invalidPost = await PostAsync(recovery, "/Conta/RedefinirSenha", new() { { "Token", "invalido" }, { "NovaSenha", "Recuperada#123!" }, { "ConfirmacaoSenha", "Recuperada#123!" } }, "/Conta/Login");
        check(invalidPost.StatusCode == HttpStatusCode.OK && Texto(await invalidPost.Content.ReadAsStringAsync()).Contains("link é inválido ou expirou"), "servidor rejeita token forjado");
        var adminHash = (await repo.ObterPorIdAsync(admin.Id))!.Senha;
        var reset = await PostAsync(recovery, "/Conta/RedefinirSenha", new() { { "Token", token }, { "NovaSenha", "Recuperada#123!" }, { "ConfirmacaoSenha", "Recuperada#123!" }, { "UsuarioId", admin.Id.ToString() }, { "Email", admin.Email } }, resetPath);
        check(reset.StatusCode == HttpStatusCode.Redirect && reset.Headers.Location?.OriginalString == "/Conta/Login" && (await repo.ObterPorIdAsync(admin.Id))!.Senha == adminHash, "link redefine somente sua conta e redireciona para login");
        check((await beforeReset.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect && (await otherReset.GetAsync("/Conta/Perfil")).StatusCode == HttpStatusCode.Redirect, "recuperação revoga todas as sessões anteriores");
        using var oldLogin = configured.Client(); using var newLogin = configured.Client();
        check((await LoginAsync(oldLogin, EmailNovo, SenhaNova)).StatusCode == HttpStatusCode.OK && (await LoginAsync(newLogin, EmailNovo, "Recuperada#123!")).StatusCode == HttpStatusCode.Redirect, "senha antiga falha e nova credencial recuperada funciona");
        var replay = await PostAsync(recovery, "/Conta/RedefinirSenha", new() { { "Token", token }, { "NovaSenha", "OutraSenha#123!" }, { "ConfirmacaoSenha", "OutraSenha#123!" } }, "/Conta/Login");
        check(replay.StatusCode == HttpStatusCode.OK && Texto(await replay.Content.ReadAsStringAsync()).Contains("link é inválido ou expirou"), "token de recuperação é de uso único");

        // Usa o mesmo Data Protection do servidor para testar validade, propósito e concorrência.
        var provider = DataProtectionProvider.Create(new DirectoryInfo(configured.Chaves), p => p.SetApplicationName("LeadScopeB2B"));
        var tokens = new RecuperacaoToken(provider);
        var current = (await repo.ObterPorEmailAsync(EmailNovo))!;
        var payload = JsonSerializer.Serialize(new RecuperacaoPayload(current.Id, current.Email, Acesso.Versao(current)));
        var expired = provider.CreateProtector(RecuperacaoToken.Proposito).ToTimeLimitedDataProtector().Protect(payload, TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);
        check(Texto(await recovery.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(expired))).Contains("link é inválido ou expirou"), "token expirado é recusado");
        var wrongPurpose = provider.CreateProtector("outro-proposito").Protect(payload);
        check(Texto(await recovery.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(wrongPurpose))).Contains("link é inválido ou expirou"), "token de outro propósito não recupera conta");
        var concurrentToken = tokens.Criar(current);
        using var resetA = configured.Client(); using var resetB = configured.Client();
        var formA = await resetA.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(concurrentToken));
        var formB = await resetB.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(concurrentToken));
        var a = resetA.PostAsync("/Conta/RedefinirSenha", new FormUrlEncodedContent(new Dictionary<string, string> { { "Token", concurrentToken }, { "NovaSenha", "ConcorrenteA#123!" }, { "ConfirmacaoSenha", "ConcorrenteA#123!" }, { "__RequestVerificationToken", Token(formA) } }));
        var b = resetB.PostAsync("/Conta/RedefinirSenha", new FormUrlEncodedContent(new Dictionary<string, string> { { "Token", concurrentToken }, { "NovaSenha", "ConcorrenteB#123!" }, { "ConfirmacaoSenha", "ConcorrenteB#123!" }, { "__RequestVerificationToken", Token(formB) } }));
        var concurrent = await Task.WhenAll(a, b);
        check(concurrent.Count(r => r.StatusCode == HttpStatusCode.Redirect) == 1 && concurrent.Count(r => r.StatusCode == HttpStatusCode.OK) == 1, "dois POSTs concorrentes não consomem o mesmo token");
        current = (await repo.ObterPorIdAsync(current.Id))!;
        var blockedToken = tokens.Criar(current);
        await usuarios.AlterarBloqueioAsync(current.Id, true);
        check(Texto(await recovery.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(blockedToken))).Contains("link é inválido ou expirou"), "conta bloqueada não pode recuperar senha");
        await usuarios.AlterarBloqueioAsync(current.Id, false);
        check(Texto(await recovery.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(blockedToken))).Contains("link é inválido ou expirou"), "desbloqueio não revalida link anterior");
        await usuarios.CadastrarAsync(new UsuarioCreateViewModel { Nome = "Bloqueado para recuperação", Email = "bloqueado-rec@example.test", Senha = Senha, PerfilAcessoId = 3 });
        var blockedUser = (await repo.ObterPorEmailAsync("bloqueado-rec@example.test"))!;
        await usuarios.AlterarBloqueioAsync(blockedUser.Id, true);
        var reqBlocked = await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", blockedUser.Email } });
        var anotherKnown = await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", EmailNovo } });
        var nextMessage = await smtp.ReceberAsync();
        check(reqBlocked.StatusCode == reqKnown.StatusCode && anotherKnown.StatusCode == reqKnown.StatusCode && nextMessage.Contains(EmailNovo) && !nextMessage.Contains(blockedUser.Email), "conta bloqueada recebe resposta genérica sem envio SMTP");
        await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", "ausente-2@example.test" } });
        check((await PostAsync(recovery, "/Conta/RecuperarAcesso", new() { { "Email", EmailNovo } })).StatusCode == HttpStatusCode.TooManyRequests, "recuperação limita solicitações independentemente da conta");
        var disabledToken = tokens.Criar((await repo.ObterPorIdAsync(blockedUser.Id))!);
        await usuarios.ExcluirAsync(blockedUser.Id);
        check(Texto(await recovery.GetStringAsync("/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(disabledToken))).Contains("link é inválido ou expirou"), "conta desativada não recupera acesso");
    }

    private static string ExtrairLink(string raw)
    {
        var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = raw[..split]; var body = raw[(split + 4)..];
        if (headers.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase)) body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
        else
        {
            body = body.Replace("=\r\n", "");
            body = Regex.Replace(body, "=([0-9A-Fa-f]{2})", m => ((char)Convert.ToByte(m.Groups[1].Value, 16)).ToString());
        }
        return Regex.Match(body, @"https?://[^\s]+").Value;
    }

    private static string? AbsolutePathOrOriginal(this Uri uri) => uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;

    private sealed class AplicacaoTeste : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _log;
        public string BaseUrl { get; }
        public string Chaves { get; }
        private AplicacaoTeste(Process process, string url, string chaves, StringBuilder log) { _process = process; BaseUrl = url; Chaves = chaves; _log = log; }
        public HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(20) };
        public static async Task<AplicacaoTeste> IniciarAsync(string root, string connection, int? smtp = null)
        {
            var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
            var url = $"http://127.0.0.1:{port}"; var chaves = Path.Combine(Path.GetTempPath(), "leadscope-keys-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(chaves);
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "bin/Debug/net10.0/LeadScopeB2B.dll"));
            start.Environment["ConnectionStrings__DefaultConnection"] = connection; start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["ASPNETCORE_URLS"] = url;
            start.Environment["DataProtection__DiretorioChaves"] = chaves;
            start.Environment["RecuperacaoAcesso__Habilitado"] = smtp.HasValue.ToString(); start.Environment["RecuperacaoAcesso__UrlPublica"] = url;
            start.Environment["EmailSmtp__Host"] = smtp.HasValue ? "127.0.0.1" : ""; start.Environment["EmailSmtp__Porta"] = (smtp ?? 587).ToString(); start.Environment["EmailSmtp__Remetente"] = "leadscope@example.test"; start.Environment["EmailSmtp__UsarTls"] = "false";
            start.Environment["EmailSmtp__Usuario"] = ""; start.Environment["EmailSmtp__Senha"] = "";
            var process = Process.Start(start)!; var log = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.AppendLine(e.Data); }; process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.AppendLine(e.Data); }; process.BeginOutputReadLine(); process.BeginErrorReadLine();
            var app = new AplicacaoTeste(process, url, chaves, log);
            try
            {
                using var client = app.Client();
                for (var i = 0; i < 100; i++)
                {
                    try { using var ready = await client.GetAsync("/Conta/Login"); if (ready.IsSuccessStatusCode) return app; } catch (HttpRequestException) { }
                    if (process.HasExited) break;
                    await Task.Delay(100);
                }
                throw new Exception("Servidor de testes de conta não iniciou.");
            }
            catch { await app.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited) { _process.Kill(true); await _process.WaitForExitAsync(); }
            _process.Dispose();
            Directory.Delete(Chaves, true);
            lock (_log) if (_log.ToString().Contains("fail:", StringComparison.OrdinalIgnoreCase)) Console.Error.WriteLine(_log.ToString());
        }
    }
}
