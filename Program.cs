using System.Threading.RateLimiting;
using LeadScopeB2B.Data;
using LeadScopeB2B.Integration;
using Microsoft.AspNetCore.DataProtection;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--criar-admin").ToArray());
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(field => $"O campo {field} deve ser um número.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) => $"O valor informado para {field} é inválido.");
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Informe um valor para este campo.");
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<ErroFormularioFilter>();
    options.Filters.Add<AuditoriaCadastroFilter>();
});
builder.Services.AddExceptionHandler<ErroHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<SqlLeadScopeB2B>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<IPessoaRepository, PessoaRepository>();
builder.Services.AddScoped<ILeadRepository, LeadRepository>();
builder.Services.AddScoped<CnaeRepository>();
builder.Services.AddScoped<AuditoriaRepository>();
builder.Services.AddScoped<ExportacaoRepository>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<EmpresaService>();
builder.Services.AddScoped<ImportacaoCsvService>();
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
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(Acesso.Edicao, p => p.RequireRole("ADMIN", "USUARIO"));
    options.AddPolicy(Acesso.Revisao, p => p.RequireRole("ADMIN", "REVISOR"));
    options.AddPolicy(Acesso.Admin, p => p.RequireRole("ADMIN"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("cadastro", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("conta", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("recuperacao", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    options.AddPolicy("redefinicao", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
// Somente o comando local pode criar o primeiro administrador; cadastro público sempre usa Usuário.
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
app.UseStatusCodePages(context => StatusPage.RenderAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
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
