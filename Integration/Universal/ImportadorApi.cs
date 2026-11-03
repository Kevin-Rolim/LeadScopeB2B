using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeadScopeB2B.Repositories;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace LeadScopeB2B.Integration.Universal;

public sealed class ImportadorApi : IImportadorApi
{
    public ImportacaoApi Importar(string documento)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(documento) > 1024 * 1024) throw new IntegracaoException("DOCUMENTO_GRANDE", "Documento limitado a 1 MiB.");
            var node = LerDocumento(documento);
            RecusarReferenciasExternas(node, 0);
            if (node["openapi"] is null)
            {
                var manifest = node.Deserialize<ContratoApi>(IntegracaoArquivoRepository.Json) ?? throw new JsonException();
                foreach (var op in manifest.Operacoes) { op.Habilitada = false; op.Revisada = false; op.Rotina.Habilitada = false; }
                return new(manifest, ["Contrato privado importado desabilitado; revise rede, autenticação, custos, mapeamento e operações."]);
            }
            var version = Texto(node, "openapi");
            if (!version.StartsWith("3.0.") && !version.StartsWith("3.1.")) throw new IntegracaoException("OPENAPI_VERSAO", "Somente OpenAPI 3.0.x e 3.1.x são suportados.");
            return ImportarOpenApi(node);
        }
        catch (IntegracaoException) { throw; }
        catch (Exception ex) when (ex is JsonException or YamlException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        { throw new IntegracaoException("DOCUMENTO_INVALIDO", "JSON/YAML inválido ou estrutura incompatível com o contrato suportado."); }
    }
    public static JsonNode LerDocumento(string text)
    {
        if (text.TrimStart().StartsWith('{')) return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 48 }) ?? throw new JsonException();
        var parser = new Parser(new StringReader(text));
        var depth = 0; var count = 0; var docs = 0;
        while (parser.MoveNext())
        {
            if (++count > 30000) throw new IntegracaoException("YAML_LIMITE", "YAML excede limite de processamento.");
            var e = parser.Current;
            if (e is AnchorAlias || e is NodeEvent n && (!n.Anchor.IsEmpty || !n.Tag.IsEmpty && !n.Tag.IsNonSpecific)) throw new IntegracaoException("YAML_RECURSO", "Aliases, anchors e tags YAML não são suportados.");
            if (e is MappingStart or SequenceStart && ++depth > 48) throw new IntegracaoException("YAML_LIMITE", "YAML excede profundidade 48.");
            if (e is MappingEnd or SequenceEnd) depth--;
            if (e is DocumentStart && ++docs > 1) throw new IntegracaoException("YAML_DOCUMENTOS", "Importe um documento por vez.");
        }
        var yaml = new YamlStream(); yaml.Load(new StringReader(text));
        if (yaml.Documents.Count != 1) throw new JsonException();
        JsonNode? Convert(YamlNode n)
        {
            if (n is YamlMappingNode map)
            {
                var result = new JsonObject();
                foreach (var pair in map.Children)
                {
                    if (pair.Key is not YamlScalarNode key || key.Value is null || result.ContainsKey(key.Value)) throw new JsonException();
                    result.Add(key.Value, Convert(pair.Value));
                }
                return result;
            }
            if (n is YamlSequenceNode seq) return new JsonArray(seq.Children.Select(Convert).ToArray());
            if (n is not YamlScalarNode scalar) throw new JsonException();
            var value = scalar.Value ?? "";
            if (scalar.Style == ScalarStyle.Plain)
            {
                if (value is "null" or "~") return null;
                if (value is "true" or "false") return JsonValue.Create(value == "true");
                if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return JsonValue.Create(number);
            }
            return JsonValue.Create(value);
        }
        return JsonNode.Parse(Convert(yaml.Documents[0].RootNode)?.ToJsonString() ?? "null") ?? throw new JsonException();
    }
    private static void RecusarReferenciasExternas(JsonNode? node, int depth)
    {
        if (depth > 48) throw new JsonException();
        if (node is JsonObject obj)
        {
            foreach (var p in obj)
            {
                if (p.Key is "$ref" or "$dynamicRef" && p.Value is not null && !p.Value.GetValue<string>().StartsWith("#/"))
                    throw new IntegracaoException("REFERENCIA_EXTERNA", "Referências externas/arquivos não são carregados. Use referências locais #/ suportadas.");
                RecusarReferenciasExternas(p.Value, depth + 1);
            }
        }
        else if (node is JsonArray array) foreach (var item in array) RecusarReferenciasExternas(item, depth + 1);
    }
    private static string Texto(JsonNode? node, string key, string padrao = "") => node?[key]?.GetValue<string>() ?? padrao;
    private sealed class LimiteResolucao { public int Nos; public long Caracteres; }
    private static JsonNode Resolver(JsonNode node, JsonNode root, HashSet<string>? trail = null, LimiteResolucao? limite = null)
    {
        trail ??= []; limite ??= new();
        limite.Caracteres += node is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text.Length : 16;
        if (++limite.Nos > 30000 || limite.Caracteres > 2 * 1024 * 1024) throw new IntegracaoException("OPENAPI_EXPANSAO", "Referências OpenAPI excedem o limite de expansão (30 mil nós / 2 milhões de caracteres).");
        if (node is JsonObject obj && obj["$ref"] is { } r)
        {
            var reference = r.GetValue<string>();
            if (!trail.Add(reference) || trail.Count > 16) throw new IntegracaoException("REF_RECURSIVA", "Referência recursiva ou com profundidade não suportada.");
            JsonNode? target = root;
            foreach (var key in reference[2..].Split('/')) target = target?[key.Replace("~1", "/").Replace("~0", "~")];
            if (target is null) throw new IntegracaoException("REF_INVALIDA", "Referência local não encontrada.");
            return Resolver(target, root, trail, limite);
        }
        if (node is JsonObject map)
        {
            var output = new JsonObject();
            foreach (var pair in map) output[pair.Key] = pair.Value is null ? null : Resolver(pair.Value, root, new(trail), limite);
            return output;
        }
        if (node is JsonArray array) return new JsonArray(array.Select(x => x is null ? null : Resolver(x, root, new(trail), limite)).ToArray());
        return node.DeepClone();
    }
    private static ImportacaoApi ImportarOpenApi(JsonNode root)
    {
        var warnings = new List<string>();
        var limite = new LimiteResolucao();
        JsonNode Resolve(JsonNode node) => Resolver(node, root, null, limite);
        var servers = root["servers"] as JsonArray;
        var server = servers?.FirstOrDefault();
        var baseUrl = Texto(server, "url");
        if (server?["variables"] is JsonObject vars)
            foreach (var v in vars) baseUrl = baseUrl.Replace("{" + v.Key + "}", Texto(v.Value, "default"));
        if (servers?.Count > 1) warnings.Add("Selecionado o primeiro servidor. Revise a URL base; servidores alternativos não são escolhidos automaticamente.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _)) throw new IntegracaoException("OPENAPI_SERVIDOR", "A especificação precisa de servidor absoluto; configure URL base explícita.");
        var c = new ContratoApi { Provedor = Texto(root["info"], "title", "API importada"), UrlBase = baseUrl };
        var schemes = root["components"]?["securitySchemes"] as JsonObject;
        var incompatAuth = new HashSet<string>();
        if (schemes is not null) foreach (var pair in schemes)
        {
            var scheme = Resolve(pair.Value!);
            var type = Texto(scheme, "type");
            var a = new AutenticacaoApi { Id = pair.Key, Nome = Texto(scheme, "name") };
            a.Tipo = type switch
            {
                "apiKey" when Texto(scheme, "in") == "header" => "apiKeyHeader", "apiKey" when Texto(scheme, "in") == "query" => "apiKeyQuery",
                "http" when Texto(scheme, "scheme").Equals("bearer", StringComparison.OrdinalIgnoreCase) => "bearer",
                "http" when Texto(scheme, "scheme").Equals("basic", StringComparison.OrdinalIgnoreCase) => "basic",
                "oauth2" when scheme["flows"]?["clientCredentials"] is not null => "clientCredentials",
                "oauth2" when scheme["flows"]?["authorizationCode"] is not null => "authorizationCode",
                _ => "unsupported"
            };
            if (a.Tipo == "unsupported") { incompatAuth.Add(pair.Key); warnings.Add($"Autenticação {pair.Key} exige adaptador específico."); continue; }
            if (a.Tipo is "clientCredentials" or "authorizationCode")
            {
                var flow = scheme["flows"]?[a.Tipo];
                a.TokenUrl = Texto(flow, "tokenUrl"); a.AutorizacaoUrl = Texto(flow, "authorizationUrl");
            }
            c.Autenticacoes.Add(a);
        }
        if (root["paths"] is not JsonObject paths || paths.Count > 200) throw new IntegracaoException("OPENAPI_PATHS", "Especificação sem paths válidos ou excedendo 200 caminhos.");
        foreach (var path in paths)
        {
            var pathItem = Resolve(path.Value!);
            foreach (var method in new[] { "get", "head", "options", "post", "put", "patch", "delete" })
            {
                if (pathItem[method] is not JsonObject operation) continue;
                var op = new OperacaoApi { Id = Texto(operation, "operationId", method + "_" + c.Operacoes.Count), Nome = Texto(operation, "summary", method.ToUpperInvariant() + " " + path.Key), Metodo = method.ToUpperInvariant(), Caminho = path.Key };
                void Incomp(string message) { op.Incompatibilidade = message; warnings.Add($"{op.Id}: {message}"); }
                try
                {
                    if (operation["servers"] is not null || pathItem["servers"] is not null) Incomp("Servidor por operação/caminho exige revisão manual da URL.");
                    var security = operation["security"] ?? root["security"];
                    if (security is JsonArray { Count: > 0 } requirements)
                    {
                        var chosen = requirements.OfType<JsonObject>().FirstOrDefault(x => x.Count == 0 || x.Count == 1 && !incompatAuth.Contains(x.First().Key));
                        if (chosen is null) Incomp("Combinação de autenticações AND ou método proprietário não suportado.");
                        else if (chosen.Count == 1)
                        {
                            op.AutenticacaoId = chosen.First().Key;
                            var scopes = chosen.First().Value as JsonArray;
                            op.Permissoes = scopes?.Select(s => s!.GetValue<string>()).ToList() ?? [];
                            var auth = c.Autenticacoes.SingleOrDefault(a => a.Id == op.AutenticacaoId);
                            if (auth is null) Incomp("Autenticação referenciada não definida.");
                            else if (auth.Tipo is "clientCredentials" or "authorizationCode") auth.Escopos = string.Join(' ', auth.Escopos.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat(op.Permissoes).Distinct());
                        }
                        if (requirements.Count > 1) warnings.Add($"{op.Id}: selecionada uma alternativa de autenticação; revise antes de ativar.");
                    }
                    var parameters = new List<JsonNode>();
                    if (pathItem["parameters"] is JsonArray pp) parameters.AddRange(pp.Select(p => Resolve(p!)));
                    if (operation["parameters"] is JsonArray po)
                        foreach (var p in po.Select(p => Resolve(p!)))
                        { parameters.RemoveAll(x => Texto(x, "name") == Texto(p, "name") && Texto(x, "in") == Texto(p, "in")); parameters.Add(p); }
                    foreach (var p in parameters)
                    {
                        if (Texto(p, "in") == "cookie" || p["content"] is not null || p["explode"]?.GetValue<bool>() == true || p["style"] is { } style && style.GetValue<string>() is not ("simple" or "form")) { Incomp("Serialização de parâmetro não suportada (cookie/content/explode/style)."); continue; }
                        var schema = Resolve(p["schema"] ?? new JsonObject { ["type"] = "string" });
                        JsonSeguro.ValidarSchemaSuportado(JsonSerializer.SerializeToElement(schema));
                        var param = Parametro(Texto(p, "name"), Texto(p, "in"), schema, p["required"]?.GetValue<bool>() == true || Texto(p, "in") == "path");
                        if (param.Local != "body" && param.Tipo is "object" or "array") Incomp("Array/objeto em rota/query/header exige adaptador de serialização.");
                        op.Parametros.Add(param);
                    }
                    if (operation["requestBody"] is { } body)
                    {
                        var resolved = Resolve(body);
                        if (resolved["content"]?["application/json"]?["schema"] is not { } bodySchema) Incomp("Somente requestBody application/json com schema é suportado.");
                        else
                        {
                            bodySchema = Resolve(bodySchema);
                            JsonSeguro.ValidarSchemaSuportado(JsonSerializer.SerializeToElement(bodySchema));
                            if (Texto(bodySchema, "type") != "object" || bodySchema["properties"] is not JsonObject properties) Incomp("Corpo raiz deve ser objeto JSON com propriedades declaradas.");
                            else
                            {
                                var required = (bodySchema["required"] as JsonArray)?.Select(x => x!.GetValue<string>()).ToHashSet() ?? [];
                                foreach (var property in properties) op.Parametros.Add(Parametro(property.Key, "body", property.Value!, required.Contains(property.Key)));
                            }
                        }
                    }
                    var responses = operation["responses"] as JsonObject;
                    var success = responses?.FirstOrDefault(p => p.Key.Length == 3 && p.Key[0] == '2').Value;
                    if (success is not null)
                    {
                        success = Resolve(success);
                        if (success["content"]?["application/json"]?["schema"] is { } schema)
                        {
                            var expanded = Resolve(schema);
                            var element = JsonSerializer.SerializeToElement(expanded);
                            JsonSeguro.ValidarSchemaSuportado(element);
                            op.SchemaResposta = element;
                        }
                        else if (op.Metodo != "HEAD" && responses?.ContainsKey("204") != true) Incomp("Resposta JSON/schema não declarados.");
                    }
                    else Incomp("Resposta de sucesso não declarada.");
                }
                catch (IntegracaoException e) { Incomp(e.Message); }
                c.Operacoes.Add(op);
                if (c.Operacoes.Count > 100) throw new IntegracaoException("OPENAPI_LIMITE", "Máximo de 100 operações por importação.");
            }
        }
        warnings.Add("Todas as operações estão desabilitadas. Mapeamento, custos, limites e permissões exigem revisão explícita.");
        return new(c, warnings);
    }
    private static ParametroApi Parametro(string name, string local, JsonNode schema, bool required)
    {
        var type = schema["type"] is JsonArray types ? types.Select(x => x!.GetValue<string>()).FirstOrDefault(x => x != "null") ?? "string" : Texto(schema, "type", "string");
        return new() { Nome = name, Local = local, Obrigatorio = required, Tipo = type, Formato = Texto(schema, "format"),
            MaxComprimento = schema["maxLength"]?.GetValue<int>() ?? 2000, Minimo = schema["minimum"]?.GetValue<decimal>(), Maximo = schema["maximum"]?.GetValue<decimal>(),
            Padrao = schema["default"] is JsonValue value ? value.ToString() : null,
            Valores = (schema["enum"] as JsonArray)?.Select(x => x?.ToString() ?? "").ToList() ?? [] };
    }
}
