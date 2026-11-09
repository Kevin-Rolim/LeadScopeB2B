using System.Net;
using System.Text.Json;
using LeadScopeB2B.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class ResendEmailTests
{
    public static async Task Executar()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailResend:ApiKey"] = "re_test",
            ["EmailResend:Remetente"] = "LeadScope <conta@example.test>"
        }).Build();
        var services = new ServiceCollection();
        services.Configure<EmailResendOptions>(config.GetSection("EmailResend"));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmailResendOptions>>();
        var handler = new Handler();
        using var client = new HttpClient(handler);
        var sender = new ResendRecuperacaoEmail(client, options);
        if (!sender.Configurado) throw new Exception("Resend deveria estar configurado.");
        await sender.EnviarAsync("pessoa@example.test", "https://example.test/reset?token=abc", "Pessoa de Teste", default);
        config["EmailResend:TemplateId"] = "recuperar-senha";
        config["EmailResend:VariavelLink"] = "CTA_LINK";
        config.Reload();
        handler.Template = true;
        await sender.EnviarAsync("pessoa@example.test", "https://example.test/reset?token=abc", "Pessoa de Teste", default);
        handler.Status = HttpStatusCode.Unauthorized;
        try { await sender.EnviarAsync("pessoa@example.test", "link", "Pessoa de Teste", default); throw new Exception("Falha HTTP ignorada."); }
        catch (HttpRequestException) { }
        config["EmailResend:ApiKey"] = "";
        config.Reload();
        if (sender.Configurado) throw new Exception("Chave vazia deveria desabilitar envio.");
        Console.WriteLine("OK: Resend — payload, autenticação, falha HTTP e configuração incompleta.");
    }

    private sealed class Handler : HttpMessageHandler
    {
        public bool Template;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Post || request.RequestUri?.AbsoluteUri != "https://api.resend.com/emails"
                || request.Headers.Authorization?.ToString() != "Bearer re_test") throw new Exception("Requisição Resend inválida.");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = json.RootElement;
            var expectedLink = Status == HttpStatusCode.OK ? "https://example.test/reset?token=abc" : "link";
            if (root.GetProperty("to")[0].GetString() != "pessoa@example.test") throw new Exception("Destinatário inválido.");
            if (Template)
            {
                if (root.TryGetProperty("text", out _) || root.TryGetProperty("html", out _)
                    || root.TryGetProperty("subject", out _)
                    || root.GetProperty("template").GetProperty("id").GetString() != "recuperar-senha"
                    || root.GetProperty("template").GetProperty("variables").GetProperty("CTA_LINK").GetString() != expectedLink
                    || root.GetProperty("template").GetProperty("variables").GetProperty("username").GetString() != "Pessoa de Teste")
                    throw new Exception("Template inválido.");
            }
            else if (!root.GetProperty("text").GetString()!.Contains(expectedLink)) throw new Exception("Texto inválido.");
            return new HttpResponseMessage(Status);
        }
    }
}
