using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using LeadScopeB2B.Integration.Universal;

namespace LeadScopeB2B.Services;

public sealed partial class ConsumoIntegracaoService
{
    internal static void NormalizarJanela(ConsumoApi uso, DateTimeOffset agora)
    {
        // Somente uma renovação explicitamente informada permite liberar a janela de headers.
        if (uso.JanelaAte <= agora) uso.RestanteJanela = null;
        if (uso.Headers?.RenovaEm is { } reset && reset <= agora)
        {
            uso.Headers.Restante = null;
            if (uso.Endpoint is null) uso.RestanteProvedor = null;
        }
    }
    public Task RegistrarCotaAsync(Guid id, OperacaoApi op, JsonElement root, CancellationToken ct)
    {
        var spec = op.ConsultaCota ?? throw new IntegracaoException("COTA_INVALIDA", "Esta operação não declara informações de consumo.");
        int? Number(string path, bool allowNull = false)
        {
            if (path == "") return null;
            var v = JsonSeguro.Selecionar(root, path);
            if (allowNull && v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var n) || n < 0)
                throw new IntegracaoException("COTA_INVALIDA", "O provedor devolveu informações de consumo incompletas ou incompatíveis.");
            return n;
        }
        var rpm = Number(spec.CaminhoRpm);
        var remaining = Number(spec.CaminhoRestante, spec.RestanteNuloSemLimite);
        var used = Number(spec.CaminhoUsado);
        var limit = Number(spec.CaminhoLimite);
        string? period = null;
        if (spec.CaminhoCompetencia != "")
        {
            var v = JsonSeguro.Selecionar(root, spec.CaminhoCompetencia);
            if (v.ValueKind != JsonValueKind.String || !Regex.IsMatch(v.GetString()!, @"^\d{4}-(0[1-9]|1[0-2])$"))
                throw new IntegracaoException("COTA_INVALIDA", "Competência do consumo não reconhecida.");
            period = v.GetString();
        }
        var unlimited = spec.LimiteZeroSemLimite && limit == 0 || spec.RestanteNuloSemLimite && spec.CaminhoRestante != "" && JsonSeguro.Selecionar(root, spec.CaminhoRestante).ValueKind == JsonValueKind.Null;
        if (spec.LimiteZeroSemLimite && limit == 0 && remaining.HasValue)
            throw new IntegracaoException("COTA_INVALIDA", "Limite ilimitado e saldo da resposta não correspondem à semântica declarada.");
        DateTimeOffset? reset = null;
        if (spec.CaminhoRenovacao != "")
        {
            var v = JsonSeguro.Selecionar(root, spec.CaminhoRenovacao);
            if (v.ValueKind != JsonValueKind.Null)
            {
                if (spec.FormatoRenovacao == "segundos" && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var seconds) && seconds >= 0) reset = DateTimeOffset.UtcNow.AddSeconds(seconds);
                else if (spec.FormatoRenovacao == "unix" && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var unix))
                {
                    try { reset = DateTimeOffset.FromUnixTimeSeconds(unix); }
                    catch (ArgumentOutOfRangeException) { throw new IntegracaoException("COTA_INVALIDA", "Renovação fora do intervalo suportado."); }
                }
                else if (spec.FormatoRenovacao == "data" && v.ValueKind == JsonValueKind.String && Regex.IsMatch(v.GetString()!, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) reset = date;
                else throw new IntegracaoException("COTA_INVALIDA", "Renovação não reconhecida; não é presumido um fuso horário.");
            }
        }
        var snapshot = new DescobertaLimitesApi { Origem = "endpoint", ObservadoEm = DateTimeOffset.UtcNow, RenovaEm = reset,
            Rpm = rpm is > 0 ? rpm : null, Restante = remaining, Usado = used, Limite = limit, Competencia = period, SemLimite = unlimited, Unidade = spec.UnidadeRestante };
        return arquivos.AlterarAsync(s =>
        {
            if (!s.Consumo.TryGetValue(id, out var u)) s.Consumo[id] = u = new();
            u.Endpoint = snapshot; u.RpmProvedor = snapshot.Rpm; u.RestanteProvedor = unlimited ? null : remaining;
            u.UnidadeCotaProvedor = snapshot.Unidade; u.CotaObservadaEm = snapshot.ObservadoEm; u.UltimaTentativaCota = snapshot.ObservadoEm; u.ErroDescoberta = null;
            return true;
        }, ct);
    }
    public Task RegistrarHeadersAsync(Guid id, OperacaoApi op, HttpResponseHeaders headers, CancellationToken ct)
    {
        string? Get(string name) => name != "" && headers.TryGetValues(name, out var list) ? list.FirstOrDefault() : null;
        int? Number(string? text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        var rpm = Number(Get(op.CabecalhoLimiteRpm)) ?? Number(Get("X-RateLimit-Limit-Minute"));
        var remaining = Number(Get(op.CabecalhoRestante));
        DateTimeOffset? reset = null; double? spacing = null;
        // Sem janela documentada, um "Limit" genérico NÃO é interpretado como RPM ou cota mensal.
        var policy = Get("RateLimit-Policy") ?? Get("RateLimit-Limit");
        var match = Regex.Match(policy ?? "", @"(?:^|,)\s*(\d{1,9})\s*;\s*w=(\d{1,9})(?:\D|$)", RegexOptions.CultureInvariant);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var quantity) && int.TryParse(match.Groups[2].Value, out var window) && quantity > 0 && window > 0)
        {
            spacing = (double)window / quantity;
            remaining ??= Number(Get("RateLimit-Remaining"));
            if (Number(Get("RateLimit-Reset")) is { } delta) reset = DateTimeOffset.UtcNow.AddSeconds(delta);
        }
        if (rpm is null && remaining is null && spacing is null) return Task.CompletedTask;
        return arquivos.AlterarAsync(s =>
        {
            if (!s.Consumo.TryGetValue(id, out var u)) s.Consumo[id] = u = new();
            if (remaining.HasValue && reset.HasValue) { u.RestanteJanela = remaining; u.JanelaAte = reset; u.UnidadeJanela = op.UnidadeCotaCabecalho; }
            u.Headers = new() { Origem = "header", ObservadoEm = DateTimeOffset.UtcNow, Rpm = rpm, Restante = remaining, RenovaEm = reset, IntervaloMinimoSegundos = spacing, Unidade = op.UnidadeCotaCabecalho };
            if (u.Endpoint is null)
            {
                if (rpm is > 0) u.RpmProvedor = rpm;
                if (remaining.HasValue) { u.RestanteProvedor = remaining; u.UnidadeCotaProvedor = op.UnidadeCotaCabecalho; }
                u.CotaObservadaEm = DateTimeOffset.UtcNow;
            }
            return true;
        }, ct);
    }
}
