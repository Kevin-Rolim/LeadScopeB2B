using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Services;

public sealed class AutenticacaoIntegracaoService(IntegracaoArquivoRepository arquivos, ApiHttpService http, DestinosHttp destinos,
    IOptions<MotorIntegracoesOptions> options) : IAutenticacaoApi
{
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private static string UrlEncode(string value) => Uri.EscapeDataString(value);
    private static void Credencial(bool ok) { if (!ok) throw new IntegracaoException("CREDENCIAL_AUSENTE", "Configure a credencial exigida pela operação."); }
    public async Task AplicarAsync(IntegracaoApi api, OperacaoApi op, ExecucaoApi execucao, HttpRequestMessage request, CancellationToken ct)
    {
        if (op.AutenticacaoId == "") return;
        var auth = api.Contrato.Autenticacoes.Single(a => a.Id == op.AutenticacaoId);
        if (auth.Tipo == "none") return;
        var s = await arquivos.LerSegredoAsync(api.Id, auth.Id, ct);
        switch (auth.Tipo)
        {
            case "apiKeyHeader": Credencial(s.Valor.Length > 0); request.Headers.Add(auth.Nome, s.Valor); break;
            case "apiKeyQuery":
                Credencial(s.Valor.Length > 0);
                var separator = request.RequestUri!.Query.Length == 0 ? "?" : "&";
                request.RequestUri = new(request.RequestUri.AbsoluteUri + separator + UrlEncode(auth.Nome) + "=" + UrlEncode(s.Valor));
                break;
            case "bearer": Credencial(s.Valor.Length > 0); request.Headers.Authorization = new("Bearer", s.Valor); break;
            case "basic":
                if (request.RequestUri!.Scheme != "https") throw new IntegracaoException("HTTPS_OBRIGATORIO", "Basic exige HTTPS.");
                Credencial(s.Usuario.Length > 0 && s.Valor.Length > 0);
                request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(s.Usuario + ":" + s.Valor))); break;
            case "clientCredentials": case "authorizationCode":
                await tokenLock.WaitAsync(ct);
                try
                {
                    s = await arquivos.LerSegredoAsync(api.Id, auth.Id, ct);
                    if (s.AccessToken.Length == 0 || s.ExpiraEm <= DateTimeOffset.UtcNow.AddSeconds(30))
                    {
                        var form = new Dictionary<string, string>();
                        if (auth.Tipo == "authorizationCode")
                        {
                            if (s.RefreshToken.Length == 0) throw new IntegracaoException("OAUTH_CONECTAR", "Conecte a conta OAuth na interface para autorizar esta integração.");
                            form["grant_type"] = "refresh_token"; form["refresh_token"] = s.RefreshToken;
                        }
                        else { form["grant_type"] = "client_credentials"; if (auth.Escopos != "") form["scope"] = auth.Escopos; }
                        s = await TrocarTokenAsync(api, op, execucao, auth, s, form, ct);
                    }
                    request.Headers.Authorization = new("Bearer", s.AccessToken);
                }
                finally { tokenLock.Release(); }
                break;
        }
    }
    private string RedirectUri()
    {
        if (!Uri.TryCreate(options.Value.UrlPublicaOAuth.TrimEnd('/') + "/IntegracoesApis/OAuthCallback", UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new IntegracaoException("OAUTH_REDIRECT", "O operador deve configurar MotorIntegracoes:UrlPublicaOAuth com a URL pública HTTPS da aplicação.");
        return uri.AbsoluteUri;
    }
    public async Task<string> IniciarPkceAsync(IntegracaoApi api, string authId, int usuarioId, CancellationToken ct, bool autorizarCusto = false, bool autorizarDesconhecido = false)
    {
        var auth = api.Contrato.Autenticacoes.Single(a => a.Id == authId && a.Tipo == "authorizationCode");
        await destinos.ValidarAsync(new(auth.AutorizacaoUrl), api.Contrato.PerfilRede, ct);
        var segredo = await arquivos.LerSegredoAsync(api.Id, authId, ct);
        Credencial(segredo.ClienteId.Length > 0);
        static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        segredo.EstadoOAuth = Base64Url(RandomNumberGenerator.GetBytes(32));
        segredo.VerificadorPkce = Base64Url(RandomNumberGenerator.GetBytes(48));
        segredo.OAuthAutorizaCusto = autorizarCusto; segredo.OAuthAutorizaDesconhecido = autorizarDesconhecido;
        segredo.UsuarioOAuth = usuarioId; segredo.EstadoExpiraEm = DateTimeOffset.UtcNow.AddMinutes(10);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(segredo.VerificadorPkce)));
        await arquivos.SalvarSegredoAsync(api.Id, authId, segredo, ct);
        var parameters = new Dictionary<string, string> { ["response_type"] = "code", ["client_id"] = segredo.ClienteId, ["redirect_uri"] = RedirectUri(), ["scope"] = auth.Escopos, ["state"] = segredo.EstadoOAuth, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256" };
        return auth.AutorizacaoUrl + (auth.AutorizacaoUrl.Contains('?') ? "&" : "?") + string.Join('&', parameters.Select(p => UrlEncode(p.Key) + "=" + UrlEncode(p.Value)));
    }
    public async Task FinalizarPkceAsync(string state, string code, int usuarioId, CancellationToken ct)
    {
        if (state.Length is < 40 or > 100 || code.Length is < 1 or > 4096) throw new IntegracaoException("OAUTH_ESTADO", "Retorno OAuth inválido.");
        await tokenLock.WaitAsync(ct);
        try
        {
            var config = await arquivos.LerAsync(ct);
            foreach (var api in config.Integracoes.Where(a => a.Habilitada)) foreach (var auth in api.Contrato.Autenticacoes.Where(a => a.Tipo == "authorizationCode"))
            {
                var segredo = await arquivos.LerSegredoAsync(api.Id, auth.Id, ct);
                if (segredo.EstadoOAuth == "" || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(segredo.EstadoOAuth))) continue;
                if (segredo.UsuarioOAuth != usuarioId || segredo.EstadoExpiraEm <= DateTimeOffset.UtcNow) throw new IntegracaoException("OAUTH_ESTADO", "Estado OAuth expirado ou pertencente a outro usuário.");
                var op = api.Contrato.Operacoes.FirstOrDefault(o => o.Habilitada && o.AutenticacaoId == auth.Id) ?? throw new IntegracaoException("OAUTH_OPERACAO", "Habilite uma operação revisada para esta autenticação.");
                var verifier = segredo.VerificadorPkce;
                segredo.EstadoOAuth = ""; segredo.VerificadorPkce = "";
                await arquivos.SalvarSegredoAsync(api.Id, auth.Id, segredo, ct); // Estado de uso único, consumido antes da troca.
                await TrocarTokenAsync(api, op, new() { UsuarioId = usuarioId, AutorizarCusto = segredo.OAuthAutorizaCusto, AutorizarCustoDesconhecido = segredo.OAuthAutorizaDesconhecido }, auth, segredo, new() { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = RedirectUri(), ["code_verifier"] = verifier }, ct);
                return;
            }
            throw new IntegracaoException("OAUTH_ESTADO", "Estado OAuth não reconhecido.");
        }
        finally { tokenLock.Release(); }
    }
    private async Task<SegredoApi> TrocarTokenAsync(IntegracaoApi api, OperacaoApi op, ExecucaoApi execucao, AutenticacaoApi auth, SegredoApi s, Dictionary<string, string> form, CancellationToken ct)
    {
        Credencial(s.ClienteId.Length > 0 && (auth.Tipo == "authorizationCode" || s.ClienteSegredo.Length > 0));
        using var request = new HttpRequestMessage(HttpMethod.Post, auth.TokenUrl);
        if (new Uri(auth.TokenUrl).Scheme != "https") throw new IntegracaoException("HTTPS_OBRIGATORIO", "Tokens OAuth exigem HTTPS.");
        if (auth.EnvioCliente == "basic" && s.ClienteSegredo.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(UrlEncode(s.ClienteId) + ":" + UrlEncode(s.ClienteSegredo))));
        else { form["client_id"] = s.ClienteId; if (s.ClienteSegredo != "") form["client_secret"] = s.ClienteSegredo; }
        request.Content = new FormUrlEncodedContent(form);
        // OAuth usa o mesmo transporte, restrições, rate limit, orçamento e auditoria. Não há retry implícito de POST.
        var response = await http.EnviarAsync(api, op, execucao, request, ct);
        if (response.Status is < 200 or >= 300) throw new IntegracaoException(response.Codigo, "Autorização OAuth recusada ou temporariamente indisponível.");
        using var json = JsonDocument.Parse(response.Corpo, new JsonDocumentOptions { MaxDepth = 16 });
        var root = json.RootElement;
        if (!root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String || token.GetString()!.Length is < 1 or > 16384 || root.TryGetProperty("token_type", out var tokenType) && !tokenType.GetString()!.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            throw new IntegracaoException("OAUTH_RESPOSTA", "Resposta OAuth não contém token Bearer suportado.");
        s.AccessToken = token.GetString()!;
        var expires = root.TryGetProperty("expires_in", out var ttl) && ttl.TryGetInt32(out var seconds) ? Math.Clamp(seconds, 1, 86400 * 365) : 60;
        s.ExpiraEm = DateTimeOffset.UtcNow.AddSeconds(expires);
        if (root.TryGetProperty("refresh_token", out var refresh) && refresh.ValueKind == JsonValueKind.String)
        {
            if (refresh.GetString()!.Length > 16384 || refresh.GetString()!.Any(char.IsControl)) throw new IntegracaoException("OAUTH_RESPOSTA", "Refresh token fora do limite suportado.");
            s.RefreshToken = refresh.GetString()!;
        }
        await arquivos.SalvarSegredoAsync(api.Id, auth.Id, s, ct);
        return s;
    }
}
