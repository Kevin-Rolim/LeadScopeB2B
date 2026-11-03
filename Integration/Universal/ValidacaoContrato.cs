using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Services;

namespace LeadScopeB2B.Integration.Universal;

public static class ValidacaoContrato
{
    private static void Exigir(bool ok, string message) { if (!ok) throw new IntegracaoException("CONTRATO_INVALIDO", message); }
    public static bool Escrita(OperacaoApi op) => op.Metodo is not ("GET" or "HEAD" or "OPTIONS");
    public static bool NomeSeguro(string value) => Regex.IsMatch(value, @"^[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant);
    public static bool NomeSensivel(string value) => Regex.IsMatch(value, "password|senha|secret|token|authorization|api.?key|cookie", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static void Validar(ContratoApi c, DestinosHttp rede)
    {
        try { ValidarInterno(c, rede); }
        catch (IntegracaoException) { throw; }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException or ArgumentException or JsonException or FormatException)
        { throw new IntegracaoException("CONTRATO_INVALIDO", "Contrato contém campos nulos, tipos ou estruturas incompatíveis."); }
    }
    private static void ValidarInterno(ContratoApi c, DestinosHttp rede)
    {
        Exigir(c.VersaoContrato == "1.0", "Versão de contrato não suportada. Use 1.0.");
        Exigir(c.Provedor.Length is > 0 and <= 100 && c.Documentacao.Length <= 2048 && c.Categoria.Length <= 100, "Identificação do provedor inválida.");
        Exigir(Uri.TryCreate(c.UrlBase, UriKind.Absolute, out var uri) && uri.Query.Length == 0, "URL base inválida; informe parâmetros separadamente.");
        rede.ValidarUri(uri!, c.PerfilRede);
        Exigir(c.Operacoes.Count is > 0 and <= 100 && c.Autenticacoes.Count <= 20, "Informe de 1 a 100 operações e até 20 autenticações.");
        Exigir(c.Operacoes.Select(x => x.Id).Distinct().Count() == c.Operacoes.Count && c.Autenticacoes.Select(x => x.Id).Distinct().Count() == c.Autenticacoes.Count, "Identificadores duplicados.");
        foreach (var auth in c.Autenticacoes)
        {
            Exigir(Regex.IsMatch(auth.Id, "^[a-zA-Z0-9_.-]{1,60}$"), "ID de autenticação inválido.");
            Exigir(new[] { "none", "apiKeyHeader", "apiKeyQuery", "bearer", "basic", "clientCredentials", "authorizationCode" }.Contains(auth.Tipo), "Método de autenticação não suportado.");
            if (auth.Tipo is "apiKeyHeader" or "apiKeyQuery") Exigir(NomeSeguro(auth.Nome) && !new[] { "Host", "Cookie", "Set-Cookie", "Content-Length", "Connection", "Transfer-Encoding" }.Contains(auth.Nome, StringComparer.OrdinalIgnoreCase), "Nome da API key inválido.");
            if (auth.Tipo == "basic") Exigir(uri!.Scheme == "https", "HTTP Basic exige HTTPS, inclusive nas redes privadas.");
            if (auth.Tipo is "clientCredentials" or "authorizationCode")
            {
                Exigir(Uri.TryCreate(auth.TokenUrl, UriKind.Absolute, out var token) && token.Scheme == "https", "OAuth exige token endpoint HTTPS.");
                Exigir(token!.Query.Length == 0, "Token endpoint deve ser informado sem query; credenciais são configuradas separadamente.");
                rede.ValidarUri(token!, c.PerfilRede);
                Exigir(auth.EnvioCliente is "basic" or "body", "Autenticação do cliente OAuth deve ser basic ou body.");
                if (auth.Tipo == "authorizationCode")
                {
                    Exigir(Uri.TryCreate(auth.AutorizacaoUrl, UriKind.Absolute, out var autorizacao) && autorizacao.Scheme == "https", "OAuth exige authorization endpoint HTTPS.");
                    Exigir(autorizacao!.Query.Length == 0, "Authorization endpoint deve ser informado sem query; parâmetros OAuth são construídos pelo motor.");
                    rede.ValidarUri(autorizacao!, c.PerfilRede);
                }
            }
        }
        Exigir(c.Erros.Count <= 50 && c.Erros.All(p => System.Text.RegularExpressions.Regex.IsMatch(p.Key, "^[45][0-9]{2}$") && NomeSeguro(p.Value)), "Declaração de erros HTTP inválida.");
        ValidarLimites(c.Capacidades.LimitesDeclarados);
        Exigir(c.Capacidades.Origem is "contrato" or "catalogo", "Origem de capacidade inválida.");
        Exigir(c.Capacidades.OperacaoCota == "" || c.Operacoes.Any(o => o.Id == c.Capacidades.OperacaoCota && o.ConsultaCota is not null), "Operação de cota não declarada.");
        ValidarLimites(c.Plano.Administrador); ValidarLimites(c.Plano.Sistema);
        Exigir(c.Plano.OrcamentoMensal is null or >= 0 && c.Plano.Mensalidade is null or >= 0 && c.Plano.Moeda.Length is >= 3 and <= 8, "Plano/orçamento inválido.");
        foreach (var op in c.Operacoes)
        {
            Exigir(NomeSeguro(op.Id) && op.Nome.Length <= 200, "ID/nome da operação inválido.");
            Exigir(new[] { "GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE" }.Contains(op.Metodo), "Método HTTP não suportado.");
            Exigir(op.Caminho.StartsWith('/') && !op.Caminho.StartsWith("//") && !op.Caminho.Contains('?') && !op.Caminho.Contains('#') && !op.Caminho.Contains('\\') && op.Caminho.Length <= 2000 && !op.Caminho.Contains(".."), "Endpoint deve ser um caminho relativo seguro, sem query ou ..");
            Exigir(op.TimeoutSegundos is >= 1 and <= 120 && op.TentativasAdicionais is >= 0 and <= 2 && op.MaxItensPorPagina is >= 1 and <= 1000, "Limites de execução inválidos.");
            Exigir(op.AutenticacaoId == "" || c.Autenticacoes.Any(a => a.Id == op.AutenticacaoId), "A operação exige autenticação não configurada.");
            Exigir(!op.Habilitada || op.Revisada && string.IsNullOrEmpty(op.Incompatibilidade), "Operação incompatível ou sem revisão não pode ser habilitada.");
            Exigir(op.Parametros.Count <= 100 && op.Parametros.Select(p => p.Nome).Distinct().Count() == op.Parametros.Count, "Parâmetros duplicados ou em excesso; use nomes únicos nesta versão.");
            foreach (var p in op.Parametros)
            {
                Exigir(NomeSeguro(p.Nome) && new[] { "path", "query", "header", "body" }.Contains(p.Local), "Nome/local de parâmetro inválido.");
                Exigir(new[] { "string", "integer", "number", "boolean", "object", "array" }.Contains(p.Tipo), "Tipo de parâmetro não suportado.");
                Exigir(p.MaxComprimento is >= 1 and <= 65536 && p.Valores.Count <= 100 && (p.Minimo is null || p.Maximo is null || p.Minimo <= p.Maximo), "Restrições de parâmetro inválidas.");
                if (p.Local == "header") Exigir(!new[] { "Authorization", "Host", "Cookie", "Set-Cookie", "Proxy-Authorization", "Content-Length", "Connection", "Transfer-Encoding", "Content-Type" }.Contains(p.Nome, StringComparer.OrdinalIgnoreCase), "Header reservado; use a configuração de autenticação.");
                Exigir(!NomeSensivel(p.Nome), "Não grave segredos em parâmetros do contrato; configure autenticação separadamente.");
                if (p.Padrao is not null) ValidarParametro(p, p.Padrao);
                Exigir(p.CampoOrigem == "" || MapeadorApi.CamposOrigem.Contains(p.CampoOrigem), "Campo de seleção automática não suportado.");
            }
            foreach (Match placeholder in Regex.Matches(op.Caminho, @"\{([^{}]+)\}"))
                Exigir(op.Parametros.Any(p => p.Nome == placeholder.Groups[1].Value && p.Local == "path" && p.Obrigatorio), "Parâmetro de rota sem definição obrigatória.");
            Exigir(!Regex.Replace(op.Caminho, @"\{[^{}]+\}", "").Contains('{') && !Regex.Replace(op.Caminho, @"\{[^{}]+\}", "").Contains('}'), "Template de rota inválido.");
            Exigir(op.CamposOmitidosAmostra.Count <= 100 && op.CamposOmitidosAmostra.All(campo => campo.Length is > 0 and <= 100), "Campos omitidos da amostra inválidos.");
            Exigir(op.Mapeamentos.Count <= 100 && op.Mapeamentos.Select(m => m.Destino).Distinct().Count() == op.Mapeamentos.Count, "Mapeamentos duplicados ou em excesso.");
            JsonSeguro.ValidarCaminho(op.CaminhoItens);
            foreach (var m in op.Mapeamentos)
            {
                JsonSeguro.ValidarCaminho(m.Caminho);
                Exigir(!NomeSensivel(m.Caminho), "Não mapeie campos de senha, token ou credenciais para entidades de negócio.");
                Exigir(MapeadorApi.Destinos.Contains(m.Destino), "Destino de mapeamento incompatível com o modelo acadêmico.");
                Exigir(new[] { "string", "number", "integer", "boolean" }.Contains(m.Tipo) && MapeadorApi.Transformacoes.Contains(m.Transformacao), "Tipo/transformação não suportado.");
            }
            if (op.Correspondencia is { } match)
            {
                JsonSeguro.ValidarCaminho(match.Caminho);
                Exigir(op.Parametros.Any(p => p.Nome == match.Parametro) && MapeadorApi.Transformacoes.Contains(match.Normalizacao), "Correspondência de identidade inválida.");
            }
            Exigir(new[] { "none", "page", "offset", "cursor" }.Contains(op.Paginacao.Tipo) && op.Paginacao.MaxPaginas is >= 1 and <= 100 && op.Paginacao.Tamanho is >= 1 and <= 1000 && op.Paginacao.Inicio >= 0, "Paginação inválida.");
            if (op.Paginacao.Tipo != "none")
            {
                Exigir(NomeSeguro(op.Paginacao.Parametro) && (op.Paginacao.ParametroTamanho == "" || NomeSeguro(op.Paginacao.ParametroTamanho)), "Parâmetro de paginação inválido.");
                if (op.Paginacao.Tipo == "cursor") JsonSeguro.ValidarCaminho(op.Paginacao.CaminhoProximoCursor);
                Exigir(!Escrita(op), "Paginação automática de operações de escrita não é suportada.");
            }
            Exigir(op.Custo.PorRequisicao is null or >= 0 && op.Custo.PorItem is null or >= 0 && op.Custo.ReservaCustoDesconhecido is null or > 0 && op.Custo.CreditosPorUnidade >= 0 && new[] { "requisicao", "item", "credito" }.Contains(op.Custo.Unidade), "Custo/unidade de consumo inválidos.");
            Exigir(op.Rotina.IntervaloMinutos is >= 1 and <= 43200 && op.Rotina.LimitePorExecucao is >= 1 and <= 100 && op.Rotina.PoliticaErros is "continuar" or "parar", "Rotina inválida.");
            Exigir(new[] { "parametrosFixos", "empresasSemEvidencias", "pessoasSemEvidencias", "empresasAtivas", "pessoasAtivas" }.Contains(op.Rotina.Criterio), "Critério de seleção não suportado.");
            Exigir(!op.Rotina.Habilitada || op.Habilitada && op.Rotina.UsuarioId > 0, "A rotina exige operação habilitada e usuário responsável.");
            Exigir(new[] { "requisicao", "item", "credito" }.Contains(op.UnidadeCotaCabecalho), "Unidade de cota inválida.");
            if (op.ConsultaCota is { } cota)
            {
                Exigir(new[] { "requisicao", "item", "credito" }.Contains(cota.UnidadeRestante), "Unidade de cota inválida.");
                Exigir(cota.AtualizarMinutos is >= 1 and <= 1440 && cota.FormatoRenovacao is "data" or "segundos" or "unix", "Janela de atualização de cota inválida.");
                foreach (var caminho in new[] { cota.CaminhoCompetencia, cota.CaminhoUsado, cota.CaminhoLimite, cota.CaminhoRenovacao }) if (caminho != "") JsonSeguro.ValidarCaminho(caminho);
                if (cota.CaminhoRpm != "") JsonSeguro.ValidarCaminho(cota.CaminhoRpm);
                if (cota.CaminhoRestante != "") JsonSeguro.ValidarCaminho(cota.CaminhoRestante);
            }
            if (op.Corpo is { } body)
            {
                Exigir(!ContemSegredo(body), "Corpo fixo não pode conter segredos de autenticação.");
                Exigir(!op.Parametros.Any(p => p.Local == "body") || body.ValueKind == JsonValueKind.Object, "Corpo raiz precisa ser objeto para receber propriedades body adicionais.");
            }
            if (op.SchemaResposta is { } schema) JsonSeguro.ValidarSchemaSuportado(schema);
        }
    }
    private static bool ContemSegredo(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.Object => node.EnumerateObject().Any(p => NomeSensivel(p.Name) && p.Value.ValueKind != JsonValueKind.Null || ContemSegredo(p.Value)),
        JsonValueKind.Array => node.EnumerateArray().Any(ContemSegredo), _ => false
    };
    private static void ValidarLimites(LimitesApi l) => Exigir(l.PorSegundo is null or > 0 && l.PorMinuto is null or > 0 && l.Diario is null or > 0 && l.Mensal is null or > 0 && l.CreditosMensais is null or >= 0, "Limites devem ser positivos (créditos podem ser zero). Desconhecido deve ser nulo.");
    public static string ValidarParametro(ParametroApi p, string value)
    {
        if (p.Formato is "cnpj" or "cnpjNumerico")
        {
            try { value = CadastroRules.Cnpj(value) ?? throw new ValidationException(); }
            catch (ValidationException) { throw new IntegracaoException("PARAMETRO_INVALIDO", $"Parâmetro {p.Nome}: CNPJ inválido."); }
            if (p.Formato == "cnpjNumerico" && value.Any(c => c is < '0' or > '9')) throw new IntegracaoException("PARAMETRO_INVALIDO", "O contrato desta operação exige CNPJ numérico.");
        }
        Exigir(value.Length <= p.MaxComprimento && (!p.Obrigatorio || value.Length > 0), $"Parâmetro {p.Nome}: tamanho inválido.");
        if (p.Tipo is "number" or "integer")
        {
            Exigir(decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && (p.Tipo != "integer" || decimal.Truncate(number) == number) && (p.Minimo is null || number >= p.Minimo) && (p.Maximo is null || number <= p.Maximo), $"Parâmetro {p.Nome}: número fora do tipo/intervalo permitido.");
        }
        if (p.Tipo == "boolean") Exigir(value is "true" or "false", $"Parâmetro {p.Nome}: booleano inválido.");
        if (p.Tipo is "object" or "array")
        {
            using var doc = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 32 });
            Exigir(doc.RootElement.ValueKind == (p.Tipo == "object" ? JsonValueKind.Object : JsonValueKind.Array), $"Parâmetro {p.Nome}: JSON de tipo inválido.");
        }
        Exigir(p.Valores.Count == 0 || p.Valores.Contains(value), $"Parâmetro {p.Nome}: valor fora da enumeração.");
        try
        {
            if (p.Formato == "cnpj") return CadastroRules.Cnpj(value) ?? throw new ValidationException();
            if (p.Formato == "email") Exigir(new EmailAddressAttribute().IsValid(value), "E-mail inválido.");
            if (p.Formato == "uri") CadastroRules.Url(value);
        }
        catch (ValidationException) { throw new IntegracaoException("PARAMETRO_INVALIDO", $"Parâmetro {p.Nome}: formato inválido."); }
        if (p.Local == "header") Exigir(!value.Any(char.IsControl), "Valor de header inválido.");
        return value;
    }
}

public static class JsonSeguro
{
    public static void ValidarCaminho(string path)
    {
        if (path.Length > 300 || !Regex.IsMatch(path, @"^\$(?:\.[A-Za-z_][A-Za-z0-9_-]*|\[(?:[0-9]{1,5}|""[^""\\\x00-\x1f]{1,100}"")\])*$", RegexOptions.CultureInvariant))
            throw new IntegracaoException("CAMINHO_JSON", "Use caminhos seguros como $.company.name, $[0].nome ou $[\"nome com espaço\"]. Filtros, scripts, recursão e curingas não são suportados.");
    }
    public static JsonElement Selecionar(JsonElement root, string path)
    {
        ValidarCaminho(path);
        foreach (Match part in Regex.Matches(path[1..], @"\.([A-Za-z_][A-Za-z0-9_-]*)|\[([0-9]+)\]|\[""([^""]+)""\]"))
        {
            if (part.Groups[2].Success)
            {
                var index = int.Parse(part.Groups[2].Value, CultureInfo.InvariantCulture);
                if (root.ValueKind != JsonValueKind.Array || index >= root.GetArrayLength()) return default;
                root = root[index];
            }
            else
            {
                var name = part.Groups[1].Success ? part.Groups[1].Value : part.Groups[3].Value;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out root)) return default;
            }
        }
        return root;
    }
    public static void ValidarSchemaSuportado(JsonElement schema)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        if (schema.ValueKind != JsonValueKind.Object) throw new IntegracaoException("SCHEMA", "Schema de resposta inválido.");
        string[] allowed = ["type", "properties", "required", "items", "additionalProperties", "nullable", "enum", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "title", "description", "default", "example", "examples", "readOnly", "writeOnly", "deprecated", "format", "$id", "$schema", "$defs", "externalDocs", "xml"];
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().ToArray() : [type];
            if (types.Length == 0 || types.Any(t => t.ValueKind != JsonValueKind.String || !new[] { "null", "string", "number", "integer", "boolean", "object", "array" }.Contains(t.GetString()))) throw new IntegracaoException("SCHEMA", "Tipo de schema inválido.");
        }
        string[] unsupported = ["$ref", "$dynamicRef", "allOf", "anyOf", "oneOf", "not", "if", "then", "else", "pattern", "patternProperties", "dependentSchemas", "unevaluatedProperties", "contains", "prefixItems"];
        foreach (var p in schema.EnumerateObject())
        {
            if (unsupported.Contains(p.Name) || !allowed.Contains(p.Name) && !p.Name.StartsWith("x-", StringComparison.Ordinal)) throw new IntegracaoException("SCHEMA_NAO_SUPORTADO", $"Schema usa recurso não suportado: {p.Name}.");
            if (p.Name == "properties" && p.Value.ValueKind != JsonValueKind.Object || p.Name is "required" or "enum" && p.Value.ValueKind != JsonValueKind.Array || p.Name == "required" && p.Value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String) || p.Name == "nullable" && p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || p.Name == "format" && p.Value.ValueKind != JsonValueKind.String)
                throw new IntegracaoException("SCHEMA", "Estrutura de schema incompatível.");
            if (p.Name == "properties" && p.Value.ValueKind == JsonValueKind.Object) foreach (var prop in p.Value.EnumerateObject()) ValidarSchemaSuportado(prop.Value);
            if (p.Name == "items" || p.Name == "additionalProperties") ValidarSchemaSuportado(p.Value);
            if (p.Name is "minLength" or "maxLength" or "minItems" or "maxItems" && (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt32(out var length) || length < 0)) throw new IntegracaoException("SCHEMA", "Restrição de tamanho inválida.");
            if (p.Name is "minimum" or "maximum" && (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetDecimal(out _))) throw new IntegracaoException("SCHEMA", "Restrição numérica inválida.");
            if (p.Name == "format" && p.Value.ValueKind == JsonValueKind.String && !new[] { "email", "uri", "url", "date", "date-time", "int32", "int64", "float", "double", "password", "byte", "binary", "uuid" }.Contains(p.Value.GetString())) throw new IntegracaoException("SCHEMA_NAO_SUPORTADO", "Formato de schema não suportado.");
        }
    }
    public static void ValidarResposta(JsonElement value, JsonElement schema, int depth = 0)
    {
        if (depth > 32) throw new IntegracaoException("SCHEMA", "Resposta excede profundidade permitida.");
        if (schema.ValueKind == JsonValueKind.True) return;
        if (schema.ValueKind == JsonValueKind.False) throw new IntegracaoException("SCHEMA", "Resposta não permitida pelo schema.");
        bool Has(string key, out JsonElement v) => schema.TryGetProperty(key, out v);
        bool Type(string t) => t switch { "null" => value.ValueKind == JsonValueKind.Null, "object" => value.ValueKind == JsonValueKind.Object, "array" => value.ValueKind == JsonValueKind.Array, "string" => value.ValueKind == JsonValueKind.String, "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False, "number" => value.ValueKind == JsonValueKind.Number, "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) && n == decimal.Truncate(n), _ => false };
        if (value.ValueKind == JsonValueKind.Null && Has("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True) return;
        if (Has("type", out var type) && !(type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Any(t => Type(t.GetString()!)) : Type(type.GetString()!))) throw new IntegracaoException("SCHEMA", "Resposta não corresponde ao tipo declarado.");
        if (Has("enum", out var values) && !values.EnumerateArray().Any(x => JsonElement.DeepEquals(x, value))) throw new IntegracaoException("SCHEMA", "Resposta fora da enumeração declarada.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (Has("required", out var required)) foreach (var key in required.EnumerateArray()) if (!value.TryGetProperty(key.GetString()!, out _)) throw new IntegracaoException("SCHEMA", "Resposta sem campo obrigatório.");
            var temPropriedades = Has("properties", out var props);
            foreach (var prop in value.EnumerateObject())
            {
                if (temPropriedades && props.TryGetProperty(prop.Name, out var s)) ValidarResposta(prop.Value, s, depth + 1);
                else if (Has("additionalProperties", out var additional)) ValidarResposta(prop.Value, additional, depth + 1);
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (Has("minItems", out var minItems) && value.GetArrayLength() < minItems.GetInt32() || Has("maxItems", out var maxItems) && value.GetArrayLength() > maxItems.GetInt32()) throw new IntegracaoException("SCHEMA", "Quantidade de itens fora do schema.");
            if (Has("items", out var items)) foreach (var item in value.EnumerateArray()) ValidarResposta(item, items, depth + 1);
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!; var length = text.Length;
            if (Has("format", out var format))
            {
                var ok = format.GetString() switch {
                    "email" => new EmailAddressAttribute().IsValid(text), "uri" or "url" => Uri.TryCreate(text, UriKind.Absolute, out _),
                    "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                    "date-time" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                    "uuid" => Guid.TryParse(text, out _), _ => true
                };
                if (!ok) throw new IntegracaoException("SCHEMA", "Formato de texto inválido na resposta.");
            }
            if (Has("maxLength", out var max) && length > max.GetInt32() || Has("minLength", out var min) && length < min.GetInt32()) throw new IntegracaoException("SCHEMA", "Tamanho de texto incompatível com o schema.");
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            if (Has("maximum", out var max) && number > max.GetDecimal() || Has("minimum", out var min) && number < min.GetDecimal()) throw new IntegracaoException("SCHEMA", "Número fora do intervalo do schema.");
    }
}
