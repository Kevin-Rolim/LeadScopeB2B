using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using LeadScopeB2B.Services;

namespace LeadScopeB2B.Integration.Universal;

public sealed class MapeadorApi : IMapeadorApi
{
    public static readonly string[] Transformacoes = ["trim", "upper", "lower", "digits", "cnpj", "url", "none"];
    public static readonly string[] Destinos = ["Empresa.RazaoSocial", "Empresa.NomeFantasia", "Empresa.Cnpj", "Empresa.UrlSite", "Empresa.Segmento", "Empresa.Porte", "Empresa.Cidade", "Empresa.Estado", "Empresa.Email", "Empresa.Telefone", "Pessoa.Nome", "Pessoa.Email", "Pessoa.Telefone", "Pessoa.UrlLinkedIn", "Pessoa.Origem", "Vinculo.Cargo", "Vinculo.Departamento", "Vinculo.Senioridade", "Cnae.Numero", "Cnae.Descricao"];
    public static readonly string[] CamposOrigem = ["Empresa.Id", "Empresa.Cnpj", "Empresa.RazaoSocial", "Empresa.Cidade", "Empresa.Estado", "Empresa.Email", "Empresa.UrlSite", "Pessoa.Id", "Pessoa.Nome", "Pessoa.Email", "Pessoa.Telefone"];
    public static string Transformar(string value, string transform) => transform switch
    {
        "trim" => value.Trim(), "none" => value, "upper" => value.Trim().ToUpperInvariant(), "lower" => value.Trim().ToLowerInvariant(),
        "digits" => new string(value.Where(c => c is >= '0' and <= '9').ToArray()), "cnpj" => CadastroRules.Cnpj(value) ?? throw new ValidationException("CNPJ ausente."),
        "url" => CadastroRules.Url(value)?.TrimEnd('/') ?? throw new ValidationException("URL ausente."),
        _ => throw new IntegracaoException("TRANSFORMACAO", "Transformação não suportada.")
    };
    public static string NormalizarDestino(string destino, string valor)
    {
        var pair = destino.Split('.');
        if (!Destinos.Contains(destino)) throw new ValidationException("Destino não permitido.");
        if (pair[0] is "Empresa" or "Pessoa") return IntegracaoService.Normalizar(pair[0] == "Pessoa", pair[1], valor);
        valor = valor.Trim();
        if (pair[0] == "Cnae")
        {
            if (pair[1] == "Numero") return CnaeService.Normalizar(valor);
            if (valor.Length is < 1 or > 255) throw new ValidationException("Descrição CNAE inválida.");
        }
        else if (valor.Length == 0 || valor.Length > (pair[1] == "Senioridade" ? 50 : 100)) throw new ValidationException("Valor de vínculo inválido.");
        return valor;
    }
    public List<Dictionary<string, string>> Mapear(JsonElement root, OperacaoApi op, List<string> avisos)
    {
        var selected = JsonSeguro.Selecionar(root, op.CaminhoItens);
        if (selected.ValueKind == JsonValueKind.Undefined) throw new IntegracaoException("MAPEAMENTO", "Caminho dos itens não encontrado na resposta.");
        var items = selected.ValueKind == JsonValueKind.Array ? selected.EnumerateArray().ToArray() : [selected];
        if (items.Length > op.MaxItensPorPagina) throw new IntegracaoException("LIMITE_ITENS", "Resposta excede o máximo de itens declarado para a reserva de consumo.");
        var output = new List<Dictionary<string, string>>();
        foreach (var item in items)
        {
            var row = new Dictionary<string, string>();
            foreach (var m in op.Mapeamentos)
            {
                try
                {
                    var value = JsonSeguro.Selecionar(item, m.Caminho);
                    var kind = value.ValueKind;
                    var valid = m.Tipo switch { "string" => kind == JsonValueKind.String, "number" => kind == JsonValueKind.Number, "integer" => kind == JsonValueKind.Number && value.TryGetDecimal(out var n) && decimal.Truncate(n) == n, "boolean" => kind is JsonValueKind.True or JsonValueKind.False, _ => false };
                    if (!valid) throw new ValidationException("Campo ausente ou tipo inválido.");
                    var text = kind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
                    row[m.Destino] = NormalizarDestino(m.Destino, Transformar(text, m.Transformacao));
                }
                catch (Exception ex) when (ex is ValidationException or IntegracaoException)
                {
                    if (m.Obrigatorio) throw new IntegracaoException("MAPEAMENTO", $"Campo obrigatório incompatível: {m.Destino}.");
                    avisos.Add($"Campo ignorado: {m.Destino} (ausente, inválido ou incompatível).");
                }
            }
            output.Add(row);
        }
        return output;
    }
}
