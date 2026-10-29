using LeadScopeB2B.Integration.Universal;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Services;

// O worker conhece apenas rotinas declaradas. O mesmo executor atende automação e consultas manuais.
public sealed class ColetaAutomaticaWorker(IServiceScopeFactory scopes, IOptions<MotorIntegracoesOptions> options,
    ILogger<ColetaAutomaticaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.AutomacaoHabilitada) logger.LogInformation("Coleta automática desabilitada por padrão; somente rotinas explicitamente ativadas na interface podem executar.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervaloVerificacaoSegundos, 1, 3600)));
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AutomacaoIntegracaoService>().ExecutarPendentesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("Falha no ciclo de integrações: {Tipo}.", ex.GetType().Name); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
