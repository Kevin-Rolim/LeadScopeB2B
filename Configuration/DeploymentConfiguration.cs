using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace LeadScopeB2B.Configuration;

public static class DeploymentConfiguration
{
    // O Docker Compose fazia esta tradução; serviços Docker no Render recebem os nomes originais.
    public static void AddDeploymentEnvironment(this ConfigurationManager configuration,
        Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var aliases = new Dictionary<string, string>
        {
            ["SQLSERVER_CONNECTION_STRING"] = "ConnectionStrings:DefaultConnection",
            ["RECUPERACAO_HABILITADO"] = "RecuperacaoAcesso:Habilitado",
            ["RECUPERACAO_PROVEDOR"] = "RecuperacaoAcesso:Provedor",
            ["RECUPERACAO_URL_PUBLICA"] = "RecuperacaoAcesso:UrlPublica",
            ["RESEND_API_KEY"] = "EmailResend:ApiKey",
            ["RESEND_REMETENTE"] = "EmailResend:Remetente",
            ["RESEND_TEMPLATE_ID"] = "EmailResend:TemplateId",
            ["RESEND_VARIAVEL_LINK"] = "EmailResend:VariavelLink",
            ["SMTP_HOST"] = "EmailSmtp:Host",
            ["SMTP_PORTA"] = "EmailSmtp:Porta",
            ["SMTP_REMETENTE"] = "EmailSmtp:Remetente",
            ["SMTP_USUARIO"] = "EmailSmtp:Usuario",
            ["SMTP_SENHA"] = "EmailSmtp:Senha",
            ["SMTP_USAR_TLS"] = "EmailSmtp:UsarTls"
        };
        var values = new Dictionary<string, string?>();
        foreach (var (alias, key) in aliases)
            if (read(alias) is string value) values[key] = value;
        if (read("RENDER_EXTERNAL_URL") is string url
            && string.IsNullOrWhiteSpace(configuration["RecuperacaoAcesso:UrlPublica"])
            && !values.ContainsKey("RecuperacaoAcesso:UrlPublica"))
            values["RecuperacaoAcesso:UrlPublica"] = url;

        // As variáveis nativas Section__Key e os argumentos de linha de comando têm prioridade.
        var index = configuration.Sources.ToList().FindLastIndex(source =>
            source is EnvironmentVariablesConfigurationSource { Prefix: null or "" });
        var source = new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = values };
        if (index < 0) configuration.Sources.Add(source);
        else configuration.Sources.Insert(index, source);
    }

    public static void ValidateRenderDatabase(IConfiguration configuration)
    {
        if (!string.Equals(configuration["RENDER"], "true", StringComparison.OrdinalIgnoreCase)) return;
        var value = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("No Render, configure ConnectionStrings__DefaultConnection com a conexão de um SQL Server acessível pelo serviço. MSSQL_SA_PASSWORD sozinho não configura nem cria o banco.");
        SqlConnectionStringBuilder connection;
        try { connection = new SqlConnectionStringBuilder(value); }
        catch (ArgumentException)
        {
            // Não incluir a connection string nem a exceção original: podem conter credenciais.
            throw new InvalidOperationException("ConnectionStrings__DefaultConnection inválida. Use uma connection string SQL Server (Server=host,porta;Database=...;User Id=...;Password=...;Encrypt=True), não uma URL PostgreSQL.");
        }
        var host = connection.DataSource.Trim();
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) host = host[4..];
        host = host.Split(',')[0].Split('\\')[0].Trim();
        if (string.IsNullOrWhiteSpace(host) || host.ToLowerInvariant() is "sqlserver" or "localhost" or "127.0.0.1" or "::1" or "[::1]" or "." or "(local)" or "host.docker.internal")
            throw new InvalidOperationException("O SQL Server configurado no Render usa um endereço local/do Docker Compose. Configure ConnectionStrings__DefaultConnection com o hostname e a porta do SQL Server remoto; o Render não inicia o sqlserver do compose.yaml.");
    }
}
