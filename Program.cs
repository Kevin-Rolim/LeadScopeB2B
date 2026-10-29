using System.Threading.RateLimiting;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Data;
using LeadScopeB2B.Integration;
using Microsoft.AspNetCore.DataProtection;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--criar-admin").ToArray());
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<ErroFormularioFilter>();
});
builder.Services.AddExceptionHandler<ErroHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<SqlLeadScopeB2B>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<IPessoaRepository, PessoaRepository>();
builder.Services.AddScoped<ILeadRepository, LeadRepository>();
builder.Services.AddScoped<CnaeRepository>();
builder.Services.AddScoped<FonteDadosRepository>();
builder.Services.AddScoped<IntegracaoRepository>();
builder.Services.AddScoped<AuditoriaRepository>();
builder.Services.AddScoped<ExportacaoRepository>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<EmpresaService>();
builder.Services.AddScoped<EnriquecimentoEmpresaService>();
builder.Services.AddScoped<PessoaService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<ContaService>();
builder.Services.Configure<EmailSmtpOptions>(builder.Configuration.GetSection("EmailSmtp"));
builder.Services.Configure<RecuperacaoAcessoOptions>(builder.Configuration.GetSection("RecuperacaoAcesso"));
builder.Services.AddScoped<IRecuperacaoEmail, SmtpRecuperacaoEmail>();
builder.Services.AddSingleton<RecuperacaoToken>();
builder.Services.AddSingleton<SolicitacoesRecuperacao>();
builder.Services.AddScoped<RecuperacaoAcessoService>();
builder.Services.AddHostedService<EnvioRecuperacaoWorker>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => { o.ValueCountLimit = 4096; o.MultipartBodyLengthLimit = 2 * 1024 * 1024; });
var protection = builder.Services.AddDataProtection().SetApplicationName("LeadScopeB2B");
if (builder.Configuration["DataProtection:DiretorioChaves"] is string keys && !string.IsNullOrWhiteSpace(keys))
    protection.PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddScoped<CnaeService>();
builder.Services.AddScoped<FonteDadosService>();
builder.Services.AddScoped<IntegracaoService>();
builder.Services.AddOptions<MotorIntegracoesOptions>().Bind(builder.Configuration.GetSection("MotorIntegracoes"))
    .PostConfigure(o => {
        if (!string.IsNullOrWhiteSpace(o.RedesPrivadasJson))
        {
            if (o.RedesPrivadasJson.Length > 32768) throw new InvalidOperationException("Configuração de rede de integrações muito grande.");
            o.RedesPrivadas = System.Text.Json.JsonSerializer.Deserialize<List<PerfilRedeApi>>(o.RedesPrivadasJson, IntegracaoArquivoRepository.Json)
                ?? throw new InvalidOperationException("Configuração de rede de integrações inválida.");
        }
    })
    .Validate(o => o.RedesPrivadas.Count <= 20 && o.RedesPrivadas.All(p => p.Id != "publica" && System.Text.RegularExpressions.Regex.IsMatch(p.Id, "^[a-zA-Z0-9_-]{1,60}$") && p.Origens.Count is > 0 and <= 20 && p.Redes.Count is > 0 and <= 20 && p.Redes.All(c => System.Net.IPNetwork.TryParse(c, out _)) && p.Origens.All(s => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "https" || p.PermitirHttp && u.Scheme == "http") && u.UserInfo.Length == 0)) && o.RedesPrivadas.Select(p=>p.Id).Distinct().Count()==o.RedesPrivadas.Count, "Perfis de rede de integrações inválidos.")
    .Validate(o => !Path.GetFullPath(o.Diretorio).StartsWith(Path.GetFullPath(builder.Environment.WebRootPath ?? "wwwroot") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Armazenamento de integrações deve ficar fora da raiz pública.")
    .ValidateOnStart();
builder.Services.AddSingleton<IntegracaoArquivoRepository>();
builder.Services.AddSingleton<DestinosHttp>();
builder.Services.AddSingleton<ControleExecucaoApi>();
builder.Services.AddSingleton<IImportadorApi, ImportadorApi>();
builder.Services.AddSingleton<ICatalogoApis, CatalogoApis>();
builder.Services.AddSingleton<IMapeadorApi, MapeadorApi>();
builder.Services.AddSingleton<ConsumoIntegracaoService>();
builder.Services.AddSingleton<IConsumoApi>(sp => sp.GetRequiredService<ConsumoIntegracaoService>());
builder.Services.AddHttpClient("Integracoes", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler {
        AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
        ConnectCallback = sp.GetRequiredService<DestinosHttp>().ConectarAsync,
        PooledConnectionLifetime = TimeSpan.Zero, MaxResponseHeadersLength = 32
    }).RemoveAllLoggers();
builder.Services.AddSingleton<ApiHttpService>();
builder.Services.AddSingleton<AutenticacaoIntegracaoService>();
builder.Services.AddSingleton<IAutenticacaoApi>(sp => sp.GetRequiredService<AutenticacaoIntegracaoService>());
builder.Services.AddScoped<IExecutorApi, ExecutorIntegracaoService>();
builder.Services.AddScoped<GerenciadorIntegracaoService>();
builder.Services.AddScoped<AutomacaoIntegracaoService>();
builder.Services.AddHostedService<ColetaAutomaticaWorker>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<ExportacaoService>();
builder.Services.AddScoped<SessaoEvents>();
builder.Services.AddScoped<IPasswordHasher<Usuarios>, PasswordHasher<Usuarios>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Conta/Login";
        options.AccessDeniedPath = "/Conta/Negado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.EventsType = typeof(SessaoEvents);
    })
    .AddScheme<AuthenticationSchemeOptions, IntegracaoAuthenticationHandler>(Acesso.Api, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(Acesso.Edicao, p => p.RequireRole("ADMIN", "OPERACAO"));
    options.AddPolicy(Acesso.Admin, p => p.RequireRole("ADMIN"));
    options.AddPolicy("EnvioIntegracao", p => p.AddAuthenticationSchemes(Acesso.Api).RequireClaim("envio", "true").RequireRole("ADMIN", "OPERACAO"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("cadastro", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("conta", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("recuperacao", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    options.AddPolicy("redefinicao", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
// Somente o comando local pode criar o primeiro administrador; cadastro público sempre usa Leitura.
if (args.Contains("--criar-admin"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<UsuarioService>().CriarAdministradorInicialAsync(
        Environment.GetEnvironmentVariable("LEADSCOPE_ADMIN_NOME") ?? "",
        Environment.GetEnvironmentVariable("LEADSCOPE_ADMIN_EMAIL") ?? "",
        Environment.GetEnvironmentVariable("LEADSCOPE_ADMIN_SENHA") ?? "");
    Console.WriteLine("Administrador inicial criado.");
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    if (http.Request.Path.StartsWithSegments("/api"))
        await Results.Problem(statusCode: http.Response.StatusCode, title: http.Response.StatusCode switch
        {
            401 => "Chave de API ausente ou inválida.",
            403 => "Acesso não permitido.",
            404 => "Recurso não encontrado.",
            405 => "Método não permitido.",
            415 => "Envie o conteúdo como application/json.",
            429 => "Limite de requisições excedido.",
            _ => "A requisição não pôde ser concluída."
        }).ExecuteAsync(http);
    else if (http.Response.StatusCode == 429)
    {
        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync("<!doctype html><html lang='pt-BR'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Aguarde - LeadScopeB2B</title><h1>Muitas tentativas</h1><p>Aguarde alguns minutos antes de tentar novamente.</p><a href='/Conta/Login'>Voltar ao acesso</a></html>");
    }
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
