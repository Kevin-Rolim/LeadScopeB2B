using System.Globalization;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Services;

namespace LeadScopeB2B.Integration.Universal;

public sealed record RespostaHttpApi(int Status, byte[] Corpo, TimeSpan? RetryAfter, string Codigo, Guid Reserva);

public sealed class ApiHttpService(IHttpClientFactory clients, DestinosHttp destinos, IConsumoApi consumo, ConsumoIntegracaoService registros)
{
    public async Task<RespostaHttpApi> EnviarAsync(IntegracaoApi api, OperacaoApi op, ExecucaoApi execucao, HttpRequestMessage request, CancellationToken ct)
    {
        using (var dnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            dnsTimeout.CancelAfter(TimeSpan.FromSeconds(op.TimeoutSegundos));
            try { await destinos.ValidarAsync(request.RequestUri!, api.Contrato.PerfilRede, dnsTimeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new IntegracaoException("TIMEOUT_DNS", "Tempo de validação DNS excedido."); }
            catch (System.Net.Sockets.SocketException) { throw new IntegracaoException("DNS", "Não foi possível resolver o destino autorizado."); }
        }
        if (request.Content is not null && (await request.Content.ReadAsByteArrayAsync(ct)).Length > 256 * 1024)
            throw new IntegracaoException("REQUISICAO_GRANDE", "Corpo da requisição limitado a 256 KiB.");
        // Uma nova conexão validada por requisição evita reutilização de sockets entre perfis de rede.
        request.Version = System.Net.HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Headers.ConnectionClose = true;
        request.Options.Set(DestinosHttp.Perfil, api.Contrato.PerfilRede);
        var reserva = await consumo.ReservarAsync(api, op, execucao, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(op.TimeoutSegundos));
        try
        {
            using var client = clients.CreateClient("Integracoes");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var retry = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;
            if ((int)response.StatusCode is 429 or 503 && retry is null) retry = TimeSpan.FromMinutes(1);
            if (retry < TimeSpan.Zero) retry = TimeSpan.Zero;
            var status = (int)response.StatusCode;
            var code = status switch
            {
                >= 200 and < 300 => "HTTP_OK", 401 when response.Headers.WwwAuthenticate.Any(x => x.Parameter?.Contains("invalid_token", StringComparison.OrdinalIgnoreCase) == true) => "TOKEN_EXPIRADO_OU_INVALIDO",
                401 => "CREDENCIAL_INVALIDA", 403 => "SEM_PERMISSAO", 429 => "RATE_LIMIT", >= 300 and < 400 => "REDIRECIONAMENTO_BLOQUEADO", >= 500 => "PROVEDOR_INDISPONIVEL", _ => "ERRO_HTTP"
            };
            int? Header(string name) => name != "" && response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
            await registros.RegistrarRespostaAsync(api.Id, reserva, status, code, retry, Header(op.CabecalhoLimiteRpm), Header(op.CabecalhoRestante), CancellationToken.None, false, Header(op.CabecalhoRestante).HasValue ? op.UnidadeCotaCabecalho : null);
            await registros.RegistrarHeadersAsync(api.Id, op, response.Headers, CancellationToken.None);
            if (!response.IsSuccessStatusCode) return new(status, [], retry, code, reserva);
            if (response.Content.Headers.ContentLength > 1024 * 1024) throw new IntegracaoException("RESPOSTA_GRANDE", "Resposta limitada a 1 MiB.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > 1024 * 1024) throw new IntegracaoException("RESPOSTA_GRANDE", "Resposta limitada a 1 MiB.");
                buffer.Write(chunk, 0, read);
            }
            return new(status, buffer.ToArray(), retry, code, reserva);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await registros.RegistrarRespostaAsync(api.Id, reserva, null, "TIMEOUT", null, null, null, CancellationToken.None);
            throw new IntegracaoException("TIMEOUT", "Tempo de requisição excedido; nenhuma repetição de escrita foi realizada.");
        }
        catch (OperationCanceledException)
        {
            await registros.RegistrarRespostaAsync(api.Id, reserva, null, "CANCELADA", null, null, null, CancellationToken.None);
            throw;
        }
        catch (IntegracaoException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or IOException)
        {
            await registros.RegistrarRespostaAsync(api.Id, reserva, null, "REDE", null, null, null, CancellationToken.None);
            throw new IntegracaoException("REDE", "Falha de rede, DNS ou TLS ao consultar o destino autorizado.");
        }
    }
}
