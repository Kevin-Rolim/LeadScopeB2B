using System.Threading.Channels;

namespace LeadScopeB2B.Services;

// A requisição pública não consulta a conta: existência e latência SMTP não vazam na resposta.
public class SolicitacoesRecuperacao
{
    private readonly Channel<string> _fila = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    public bool Enfileirar(string email) => _fila.Writer.TryWrite(email);
    public IAsyncEnumerable<string> LerAsync(CancellationToken ct) => _fila.Reader.ReadAllAsync(ct);
}

public class EnvioRecuperacaoWorker(SolicitacoesRecuperacao fila, IServiceScopeFactory scopes, ILogger<EnvioRecuperacaoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in fila.LerAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecuperacaoAcessoService>().EnviarAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Não incluir destinatário, link, token ou credenciais nas mensagens de log.
                logger.LogError("Não foi possível concluir uma recuperação de acesso. Tipo: {Tipo}", ex.GetType().Name);
            }
        }
    }
}
