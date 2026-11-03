using System.Text.Json;
using LeadScopeB2B.Integration.Universal;

namespace LeadScopeB2B.Services;

public sealed partial class ExecutorIntegracaoService
{
    private static OperacaoApi? OperacaoCota(IntegracaoApi api) => api.Contrato.Operacoes.FirstOrDefault(o => o.ConsultaCota is not null &&
        (api.Contrato.Capacidades.OperacaoCota == "" || o.Id == api.Contrato.Capacidades.OperacaoCota));
    private async Task AtualizarCotaInternaAsync(IntegracaoApi api, int usuarioId, CancellationToken ct)
    {
        var op = OperacaoCota(api);
        // A descoberta nunca tenta adivinhar endpoints nem executar operações de custo desconhecido/escrita.
        if (op is null || !op.Habilitada || !op.Revisada || op.Metodo != "GET" || op.Custo.PorRequisicao != 0 || op.Custo.PorItem != 0) return;
        var state = await arquivos.LerAsync(ct);
        var u = state.Consumo.GetValueOrDefault(api.Id);
        if (u?.BloqueadaAte > DateTimeOffset.UtcNow) return;
        var intervalo = u?.ErroDescoberta is null ? op.ConsultaCota!.AtualizarMinutos : 5;
        if (u?.UltimaTentativaCota is { } last && last.AddMinutes(intervalo) > DateTimeOffset.UtcNow && !(u.Endpoint?.RenovaEm <= DateTimeOffset.UtcNow))
        {
            if (u.ErroDescoberta is {} erro) throw new IntegracaoException(erro,"A última verificação de consumo falhou. Aguarde a próxima tentativa ou confira a credencial; a consulta empresarial não foi enviada.");
            return;
        }
        await arquivos.AlterarAsync(s => { if (!s.Consumo.TryGetValue(api.Id, out var usage)) s.Consumo[api.Id] = usage = new(); usage.UltimaTentativaCota = DateTimeOffset.UtcNow; return true; }, ct);
        try
        {
            var args = PrepararParametros(op, []);
            using var request = Construir(api, op, args, op.Paginacao.Inicio, "");
            var execution = new ExecucaoApi { IntegracaoId = api.Id, OperacaoId = op.Id, UsuarioId = usuarioId };
            await auth.AplicarAsync(api, op, execution, request, ct);
            var response = await http.EnviarAsync(api, op, execution, request, ct);
            if (response.Status is < 200 or >= 300) throw new IntegracaoException(response.Codigo, "Não foi possível atualizar o consumo: HTTP " + response.Status + ".");
            using var json = JsonDocument.Parse(response.Corpo, new JsonDocumentOptions { MaxDepth = 32 });
            await consumo.RegistrarCotaAsync(api.Id, op, json.RootElement, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IntegracaoException or JsonException)
        {
            var code = ex is IntegracaoException e ? e.Codigo : "COTA_INVALIDA";
            await arquivos.AlterarAsync(s => { s.Consumo[api.Id].ErroDescoberta = code; return true; }, CancellationToken.None);
            // Não gasta consultas de negócio quando uma credencial/cota não pôde ser verificada.
            throw new IntegracaoException(code, "Não foi possível atualizar as informações do provedor. Aguarde ou confira a credencial.");
        }
    }
    public async Task AtualizarConsumoAsync(Guid id, int usuarioId, CancellationToken ct)
    {
        await controle.Exclusao.WaitAsync(ct);
        try
        {
            if (!await dados.UsuarioPodeIntegrarAsync(usuarioId, ct)) throw new IntegracaoException("SEM_PERMISSAO", "Acesso não permitido.");
            var api = (await arquivos.LerAsync(ct)).Integracoes.SingleOrDefault(a=>a.Id==id);
            if (api is not null && api.Habilitada && api.Contrato.Plano.UsoAutorizado) await AtualizarCotaInternaAsync(api, usuarioId, ct);
        }
        finally { controle.Exclusao.Release(); }
    }
    public async Task AtualizarConsumosPendentesAsync(CancellationToken ct)
    {
        var state = await arquivos.LerAsync(ct);
        foreach (var api in state.Integracoes.Where(a => a.Habilitada && (a.AutomacaoPelaInterface || options.Value.AutomacaoHabilitada)))
        {
            var rotina = api.Contrato.Operacoes.FirstOrDefault(o => o.Habilitada && o.Rotina.Habilitada);
            if (rotina is null || !await dados.UsuarioPodeIntegrarAsync(rotina.Rotina.UsuarioId, ct)) continue;
            await controle.Exclusao.WaitAsync(ct);
            try { await AtualizarCotaInternaAsync(api, rotina.Rotina.UsuarioId, ct); }
            catch (IntegracaoException) { /* Persistida; próximas tentativas respeitam cooldown e cache. */ }
            finally { controle.Exclusao.Release(); }
        }
    }
}
