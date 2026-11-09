using LeadScopeB2B.Configuration;
using LeadScopeB2B.Services;
using Microsoft.Extensions.Configuration;

internal static class DeploymentConfigurationTests
{
    public static void Executar()
    {
        var env = new Dictionary<string, string?>
        {
            ["RECUPERACAO_HABILITADO"] = "true",
            ["RECUPERACAO_PROVEDOR"] = "Resend",
            ["RESEND_API_KEY"] = "re_test",
            ["RESEND_REMETENTE"] = "conta@example.test",
            ["RENDER_EXTERNAL_URL"] = "https://app.onrender.com",
            ["SQLSERVER_CONNECTION_STRING"] = "Server=db.example.test,1433;Database=LeadScopeB2B;User Id=test;Password=test;Encrypt=True"
        };
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RecuperacaoAcesso:Habilitado"] = "false",
            ["RecuperacaoAcesso:Provedor"] = "Smtp",
            ["RecuperacaoAcesso:UrlPublica"] = "",
            ["RENDER"] = "true"
        });
        config.AddDeploymentEnvironment(key => env.GetValueOrDefault(key));
        var recovery = config.GetSection("RecuperacaoAcesso").Get<RecuperacaoAcessoOptions>()!;
        if (!recovery.Habilitado || recovery.Provedor != "Resend" || recovery.UrlPublica != env["RENDER_EXTERNAL_URL"]
            || config["EmailResend:ApiKey"] != "re_test" || config["EmailResend:Remetente"] != env["RESEND_REMETENTE"])
            throw new Exception("As variáveis do Compose não configuraram a recuperação no Render.");
        DeploymentConfiguration.ValidateRenderDatabase(config);
        var precedence = new ConfigurationManager();
        precedence.AddEnvironmentVariables();
        precedence.AddInMemoryCollection(new Dictionary<string, string?> { ["RecuperacaoAcesso:Habilitado"] = "false" });
        precedence.AddDeploymentEnvironment(key => env.GetValueOrDefault(key));
        if (precedence.GetValue<bool>("RecuperacaoAcesso:Habilitado"))
            throw new Exception("Alias sobrescreveu uma fonte com prioridade maior.");
        foreach (var connection in new[] { "", "Server=sqlserver,1433", "Server=tcp:localhost,1433", "postgresql://secret@db/banco" })
        {
            config["ConnectionStrings:DefaultConnection"] = connection;
            try { DeploymentConfiguration.ValidateRenderDatabase(config); }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("secret")) throw new Exception("Diagnóstico expôs credenciais.");
                continue;
            }
            throw new Exception("Conexão inválida para Render foi aceita.");
        }
        config["RENDER"] = "false";
        config["ConnectionStrings:DefaultConnection"] = "Server=sqlserver,1433";
        DeploymentConfiguration.ValidateRenderDatabase(config);
        Console.WriteLine("OK: configuração Render — aliases, recuperação, prioridades e conexões inválidas sem expor segredos.");
    }
}
