using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;

namespace LeadScopeB2B.Services;

public sealed class AutomacaoIntegracaoService(IntegracaoArquivoRepository arquivos, IntegracaoRepository dados, IExecutorApi executor,
    ILogger<AutomacaoIntegracaoService> logger, Microsoft.Extensions.Options.IOptions<MotorIntegracoesOptions> options)
{
    public async Task ExecutarPendentesAsync(CancellationToken ct)
    {
        await executor.AtualizarConsumosPendentesAsync(ct);
        var state = await arquivos.LerAsync(ct);
        foreach (var api in state.Integracoes.Where(a => a.Habilitada && (options.Value.AutomacaoHabilitada || a.AutomacaoPelaInterface))) foreach (var op in api.Contrato.Operacoes.Where(o => o.Habilitada && o.Rotina.Habilitada))
        {
            ct.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            if (state.Consumo.TryGetValue(api.Id, out var usage) && (usage.BloqueadaAte > now || usage.ProximaRotina.GetValueOrDefault(op.Id) > now)) continue;
            // Reserva o próximo ciclo antes de fazer HTTP. Reinício não repete imediatamente um ciclo interrompido.
            var reserved = await arquivos.AlterarAsync(s =>
            {
                var current = s.Integracoes.SingleOrDefault(a => a.Id == api.Id);
                if (current?.Revisao != api.Revisao || current.Habilitada != true) return false;
                if (!s.Consumo.TryGetValue(api.Id, out var u)) s.Consumo[api.Id] = u = new();
                if (u.ProximaRotina.GetValueOrDefault(op.Id) > DateTimeOffset.UtcNow) return false;
                u.ProximaRotina[op.Id] = DateTimeOffset.UtcNow.AddMinutes(op.Rotina.IntervaloMinutos);
                return true;
            }, ct);
            if (!reserved) continue;
            try
            {
                if (!await dados.UsuarioPodeIntegrarAsync(op.Rotina.UsuarioId, ct)) continue;
                List<DestinoDadosApi?> destinos = [null];
                if (op.Rotina.Criterio != "parametrosFixos")
                {
                    var apenasSemEvidencias = op.Rotina.Criterio.EndsWith("SemEvidencias", StringComparison.Ordinal);
                    if ((apenasSemEvidencias || op.Rotina.PersistirEvidencias) && (api.FonteDadosId <= 0 || !await dados.FonteIntegracaoAtivaAsync(api.FonteDadosId, ct))) continue;
                    var candidates = await dados.CandidatosIntegracaoAsync(api.FonteDadosId, op.Rotina.Criterio.StartsWith("pessoas", StringComparison.Ordinal), ct, apenasSemEvidencias);
                    var last = usage?.UltimoRegistroRotina.GetValueOrDefault(op.Id) ?? 0;
                    destinos = candidates.Where(d => (d.EmpresaId ?? d.PessoaId) > last).Concat(candidates.Where(d => (d.EmpresaId ?? d.PessoaId) <= last)).Take(op.Rotina.LimitePorExecucao).Cast<DestinoDadosApi?>().ToList();
                }
                if (destinos.Count==0)
                {
                    // Um ciclo vazio não deve deixar empresas cadastradas depois aguardando 12 horas.
                    await arquivos.AlterarAsync(s=>{s.Consumo[api.Id].ProximaRotina[op.Id]=DateTimeOffset.UtcNow.AddMinutes(1);return true;},ct);
                }
                foreach (var destination in destinos)
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await executor.ExecutarAsync(new() {
                        IntegracaoId = api.Id, OperacaoId = op.Id, UsuarioId = op.Rotina.UsuarioId, Automatica = true,
                        AutorizarEscrita = op.Rotina.PermitirEscrita, AutorizarCusto = op.Rotina.AutorizarCusto, AutorizarCustoDesconhecido = op.Rotina.AutorizarCustoDesconhecido,
                        PersistirEvidencias = op.Rotina.PersistirEvidencias, Parametros = new(op.Rotina.Parametros), EmpresaId = destination?.EmpresaId, PessoaId = destination?.PessoaId
                    }, ct);
                    if (destination is not null) await arquivos.AlterarAsync(s => { s.Consumo[api.Id].UltimoRegistroRotina[op.Id] = (destination.EmpresaId ?? destination.PessoaId)!.Value; return true; }, ct);
                    logger.LogInformation("Rotina de integração {IntegracaoId}/{OperacaoId}: {Codigo}.", api.Id, op.Id, result.Codigo);
                    if (!result.Sucesso && (op.Rotina.PoliticaErros == "parar" || result.Codigo is "RATE_LIMIT" or "RETRY_AFTER" or "COTA_LOCAL" or "COTA_PROVEDOR" or "ORCAMENTO" or "CREDENCIAL_INVALIDA" or "TOKEN_EXPIRADO_OU_INVALIDO" or "SEM_PERMISSAO" or "JANELA_PROVEDOR" or "LIMITE_DOCUMENTADO")) break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning("Rotina {IntegracaoId}/{OperacaoId} interrompida: {Tipo}.", api.Id, op.Id, ex.GetType().Name); }
        }
    }
}
