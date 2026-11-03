using System.Globalization;
using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;

namespace LeadScopeB2B.Services;

public sealed partial class ConsumoIntegracaoService(IntegracaoArquivoRepository arquivos) : IConsumoApi
{
    private static int? Menor(params int?[] values) => values.Where(x => x.HasValue).Select(x => x!.Value).Cast<int?>().DefaultIfEmpty().Min();
    private static decimal? Menor(params decimal?[] values) => values.Where(x => x.HasValue).Select(x => x!.Value).Cast<decimal?>().DefaultIfEmpty().Min();
    public async Task<Guid> ReservarAsync(IntegracaoApi api, OperacaoApi op, ExecucaoApi execucao, CancellationToken ct)
    {
        while (true)
        {
            var decision = await arquivos.AlterarAsync(state =>
            {
                var atual = state.Integracoes.SingleOrDefault(x => x.Id == api.Id);
                if (atual is null || !atual.Habilitada || atual.Revisao != api.Revisao || !atual.Contrato.Plano.UsoAutorizado)
                    throw new IntegracaoException("CONFIGURACAO_ALTERADA", "Integração desabilitada, não autorizada ou alterada durante a execução.");
                var now = DateTimeOffset.UtcNow;
                if (!state.Consumo.TryGetValue(api.Id, out var usage)) state.Consumo[api.Id] = usage = new();
                if (usage.BloqueadaAte > now) throw new IntegracaoException("RETRY_AFTER", "Integração em espera até " + usage.BloqueadaAte.ToString("u") + ".");
                NormalizarJanela(usage, now);
                var limits = api.Contrato.Plano;
                var credits = op.Custo.CreditosPorUnidade * (op.Custo.Unidade == "item" ? op.MaxItensPorPagina : 1);
                var unidades = usage.UnidadeCotaProvedor switch { "item" => op.MaxItensPorPagina, "credito" => decimal.Ceiling(credits), _ => 1m };
                if (unidades > int.MaxValue) throw new IntegracaoException("CREDITOS", "Consumo declarado excede o máximo suportado.");
                var consumoProvedor = (int)unidades;
                var rpm = Menor(limits.Administrador.PorMinuto, limits.Sistema.PorMinuto, (usage.Endpoint?.Rpm ?? usage.Headers?.Rpm ?? usage.RpmProvedor ?? api.Contrato.Capacidades.LimitesDeclarados.PorMinuto)) ?? 6;
                var rps = Menor(limits.Administrador.PorSegundo, limits.Sistema.PorSegundo, api.Contrato.Capacidades.LimitesDeclarados.PorSegundo);
                var daily = Menor(limits.Administrador.Diario, limits.Sistema.Diario) ?? 20;
                var monthly = Menor(limits.Administrador.Mensal, limits.Sistema.Mensal) ?? 200;
                var dia = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var mes = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var gerencial = op.ConsultaCota?.NaoConsomeCota == true && op.Metodo == "GET" && op.Custo.PorRequisicao == 0 && op.Custo.PorItem == 0;
                var gerenciaisMes = usage.ConsultasGerenciaisDia.Where(p=>p.Key.StartsWith(mes,StringComparison.Ordinal)).Sum(p=>(long)p.Value);
                var chamadasMes = usage.RequisicoesDia.Where(p=>p.Key.StartsWith(mes,StringComparison.Ordinal)).Sum(p=>(long)p.Value);
                if (gerencial ? usage.ConsultasGerenciaisDia.GetValueOrDefault(dia)>=100 || gerenciaisMes>=2000
                    : usage.RequisicoesDia.GetValueOrDefault(dia)-usage.ConsultasGerenciaisDia.GetValueOrDefault(dia)>=daily || chamadasMes-gerenciaisMes>=monthly)
                    throw new IntegracaoException("COTA_LOCAL", "A proteção local de consultas foi atingida. Isso não identifica a cota contratada com o provedor.");
                // Sem fuso/janela externa declarados, não reinicia cotas documentadas à meia-noite UTC.
                var declarados = api.Contrato.Capacidades.LimitesDeclarados;
                long Desde(string inicio) => usage.RequisicoesDia.Where(p=>string.CompareOrdinal(p.Key,inicio)>=0).Sum(p=>(long)p.Value-usage.ConsultasGerenciaisDia.GetValueOrDefault(p.Key));
                if (!gerencial && (declarados.Diario is {} docDia && Desde(now.AddDays(-1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))>=docDia || declarados.Mensal is {} docMes && Desde(now.AddDays(-32).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))>=docMes))
                    throw new IntegracaoException("LIMITE_DOCUMENTADO", "Limite documentado atingido pela avaliação conservadora. O provedor não informou uma renovação exata.");
                if (op.ConsultaCota is null && usage.RestanteProvedor.HasValue && usage.RestanteProvedor.Value < consumoProvedor) throw new IntegracaoException("COTA_PROVEDOR", "O provedor informou cota esgotada. Atualize-a pela operação de consulta de cota quando disponível.");
                var consumoJanela = usage.UnidadeJanela switch { "item" => op.MaxItensPorPagina, "credito" => decimal.Ceiling(credits), _ => 1m };
                if (op.ConsultaCota is null && usage.RestanteJanela is { } saldo && usage.JanelaAte is { } renovacao && renovacao > now && saldo < consumoJanela)
                    throw new IntegracaoException("JANELA_PROVEDOR", "O provedor limitou temporariamente as chamadas até " + renovacao.ToString("u") + ".");
                var unknown = op.Custo.PorRequisicao is null || op.Custo.PorItem is null;
                decimal reserva;
                try { reserva = unknown ? op.Custo.ReservaCustoDesconhecido ?? 0 : checked(op.Custo.PorRequisicao!.Value + op.Custo.PorItem!.Value * op.MaxItensPorPagina); }
                catch (OverflowException) { throw new IntegracaoException("CUSTO", "Estimativa de custo fora do limite suportado."); }
                if (unknown && !execucao.AutorizarCustoDesconhecido)
                    throw new IntegracaoException("CUSTO_DESCONHECIDO", "O provedor não informa o preço desta operação. Confirme o consumo para continuar; a automação exige autorização própria.");
                if (reserva > 0 && !execucao.AutorizarCusto)
                    throw new IntegracaoException("ORCAMENTO", "Confirme a operação cobrada antes de executar.");
                if (unknown && limits.OrcamentoMensal.HasValue && op.Custo.ReservaCustoDesconhecido is null)
                    throw new IntegracaoException("ORCAMENTO", "O teto financeiro está ativo, mas o preço é desconhecido. Defina uma reserva conservadora nas opções avançadas ou mantenha a operação suspensa.");
                var gasto = usage.CustoMes.GetValueOrDefault(mes);
                if (limits.OrcamentoMensal.HasValue && gasto + reserva + (limits.Mensalidade ?? 0) > limits.OrcamentoMensal)
                    throw new IntegracaoException("ORCAMENTO", "Orçamento mensal insuficiente para a reserva conservadora.");
                var creditLimit = Menor(limits.Administrador.CreditosMensais, limits.Sistema.CreditosMensais);
                if (creditLimit.HasValue && usage.CreditosMes.GetValueOrDefault(mes) + credits > creditLimit)
                    throw new IntegracaoException("CREDITOS", "Créditos mensais locais insuficientes.");
                if (usage.ProximaRequisicao > now) return (Id: Guid.Empty, Espera: usage.ProximaRequisicao - now);
                var seconds = Math.Max(rps.HasValue ? 1d / rps.Value : 0, 60d / rpm) + .05;
                seconds = Math.Max(seconds, usage.Endpoint?.IntervaloMinimoSegundos ?? usage.Headers?.IntervaloMinimoSegundos ?? 0);
                usage.ProximaRequisicao = now.AddSeconds(seconds);
                usage.RequisicoesDia[dia] = usage.RequisicoesDia.GetValueOrDefault(dia) + 1;
                if (gerencial) usage.ConsultasGerenciaisDia[dia] = usage.ConsultasGerenciaisDia.GetValueOrDefault(dia) + 1;
                usage.CustoMes[mes] = gasto + reserva;
                if (unknown) usage.ConsumoCustoDesconhecido[mes] = usage.ConsumoCustoDesconhecido.GetValueOrDefault(mes) + 1;
                usage.CreditosMes[mes] = usage.CreditosMes.GetValueOrDefault(mes) + credits;
                if (op.ConsultaCota is null && usage.RestanteJanela.HasValue && usage.JanelaAte > now) usage.RestanteJanela -= (int)consumoJanela;
                if (op.ConsultaCota is null && usage.RestanteProvedor.HasValue) usage.RestanteProvedor -= consumoProvedor;
                var h = new HistoricoApi { IntegracaoId = api.Id, OperacaoId = op.Id, UsuarioId = execucao.UsuarioId, Reserva = reserva, Creditos = credits, CustoDesconhecido = unknown };
                state.Historico.Add(h);
                if (state.Historico.Count > 2000) state.Historico.RemoveRange(0, state.Historico.Count - 2000);
                // Agregados não dependem da retenção do histórico e sobrevivem aos reinícios.
                foreach (var key in usage.RequisicoesDia.Keys.Where(k => string.CompareOrdinal(k, now.AddMonths(-2).ToString("yyyy-MM-dd")) < 0).ToArray()) { usage.RequisicoesDia.Remove(key); usage.ConsultasGerenciaisDia.Remove(key); }
                return (Id: h.Id, Espera: TimeSpan.Zero);
            }, ct);
            if (decision.Id != Guid.Empty) return decision.Id;
            await Task.Delay(decision.Espera, ct);
        }
    }
    public Task RegistrarRespostaAsync(Guid api, Guid reserva, int? status, string codigo, TimeSpan? retry, int? rpm, int? restante, CancellationToken ct, bool limparRestante = false, string? unidadeRestante = null) => arquivos.AlterarAsync(state =>
    {
        var h = state.Historico.SingleOrDefault(x => x.Id == reserva);
        if (h is not null) { h.Status = status; h.Codigo = codigo; }
        if (!state.Consumo.TryGetValue(api, out var usage)) state.Consumo[api] = usage = new();
        if (retry.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            var seconds = Math.Clamp(retry.Value.TotalSeconds, 0, (DateTimeOffset.MaxValue - now).TotalSeconds - 1);
            var until = now.AddSeconds(seconds);
            if (until > usage.BloqueadaAte) usage.BloqueadaAte = until;
        }
        if (usage.Endpoint is null && rpm is > 0) usage.RpmProvedor = rpm;
        if (usage.Endpoint is null && restante is >= 0) usage.RestanteProvedor = restante;
        if (usage.Endpoint is null && unidadeRestante is not null) usage.UnidadeCotaProvedor = unidadeRestante;
        if (limparRestante) usage.RestanteProvedor = null;
        if (rpm.HasValue || restante.HasValue || limparRestante) usage.CotaObservadaEm = DateTimeOffset.UtcNow;
        return true;
    }, ct);
}
