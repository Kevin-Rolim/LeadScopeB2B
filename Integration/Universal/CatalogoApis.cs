namespace LeadScopeB2B.Integration.Universal;

// Exemplos declarativos verificados contra documentação. Não são parcerias/certificações oficiais.
public sealed class CatalogoApis : ICatalogoApis
{
    public IReadOnlyList<ContratoApi> Listar() =>
    [
        new() {
            Provedor = "CNPJAPI", Categoria = "Dados empresariais", Documentacao = "https://cnpjapi.com.br/docs", UrlBase = "https://api.cnpjapi.com.br",
            Capacidades = new() { OperacaoCota = "consultarCota", Origem = "catalogo", Documentacao = "https://cnpjapi.com.br/docs/cota", VerificadoEm = new DateTimeOffset(2026,10,26,0,0,0,TimeSpan.Zero) },
            Autenticacoes = [new() { Id = "chave", Tipo = "bearer" }],
            Operacoes = [new() {
                CamposOmitidosAmostra = ["QSA"],
                Id = "consultarCnpj", Nome = "Consultar empresa por CNPJ", Caminho = "/{cnpj}", AutenticacaoId = "chave",
                Parametros = [new() { Nome = "cnpj", Local = "path", Obrigatorio = true, MaxComprimento = 14, Formato = "cnpjNumerico", CampoOrigem = "Empresa.Cnpj" }],
                Correspondencia = new() { Parametro = "cnpj", Caminho = "$.CNPJ", Normalizacao = "cnpj" },
                Mapeamentos = [new() { Caminho = "$.CNPJ", Destino = "Empresa.Cnpj", Transformacao = "cnpj", Obrigatorio = true },
                    new() { Caminho = "$.RazaoSocial", Destino = "Empresa.RazaoSocial", Obrigatorio = true }, new() { Caminho = "$.NomeFantasia", Destino = "Empresa.NomeFantasia" },
                    new() { Caminho = "$.Porte.Descricao", Destino = "Empresa.Porte" }, new() { Caminho = "$.AtividadePrincipal.Descricao", Destino = "Empresa.Segmento" },
                    new() { Caminho = "$.Contato.Email", Destino = "Empresa.Email", Transformacao = "lower" }, new() { Caminho = "$.Endereco.Municipio", Destino = "Empresa.Cidade" },
                    new() { Caminho = "$.Endereco.UF", Destino = "Empresa.Estado", Transformacao = "upper" }],
                Rotina = new() { Criterio = "empresasSemEvidencias", PersistirEvidencias = true }
            }, new() {
                Id = "consultarCota", Nome = "Consultar cota oficial da conta", Caminho = "/cota", AutenticacaoId = "chave",
                ConsultaCota = new() { NaoConsomeCota = true, CaminhoRpm = "$.rpm", CaminhoRestante = "$.restante", RestanteNuloSemLimite = true, CaminhoCompetencia = "$.competencia", CaminhoUsado = "$.usado", CaminhoLimite = "$.limite", LimiteZeroSemLimite = true },
                Custo = new() { PorRequisicao = 0, PorItem = 0, CreditosPorUnidade = 0 }
            }]
        },
        new() {
            Provedor = "ViaCEP", Categoria = "Dados geográficos", Documentacao = "https://viacep.com.br/", UrlBase = "https://viacep.com.br",
            Capacidades = new() { GratuidadeDocumentada = true, Origem = "catalogo", Documentacao = "https://viacep.com.br/", VerificadoEm = new DateTimeOffset(2026,10,26,0,0,0,TimeSpan.Zero) },
            Plano = new() { Mensalidade = 0, CondicoesUso = "Consultas de CEP; o fornecedor informa que uso massivo pode ser bloqueado. Defina limites conservadores." },
            Operacoes = [new() { Id = "consultarCep", Nome = "Consultar CEP", Caminho = "/ws/{cep}/json/",
                Parametros = [new() { Nome = "cep", Local = "path", Tipo = "integer", Obrigatorio = true, MaxComprimento = 8, Minimo = 0, Maximo = 99999999 }],
                Mapeamentos = [new() { Caminho = "$.localidade", Destino = "Empresa.Cidade", Obrigatorio = true }, new() { Caminho = "$.uf", Destino = "Empresa.Estado", Transformacao = "upper", Obrigatorio = true }],
                Custo = new() { PorRequisicao = 0, PorItem = 0 }
            }]
        },
        new() {
            Provedor = "IBGE Localidades", Categoria = "Informações públicas governamentais", Documentacao = "https://servicodados.ibge.gov.br/api/docs/localidades", UrlBase = "https://servicodados.ibge.gov.br/api/v1/localidades",
            Operacoes = [new() { Id = "consultarMunicipio", Nome = "Consultar município pelo código IBGE", Caminho = "/municipios/{id}",
                Parametros = [new() { Nome = "id", Local = "path", Tipo = "integer", Obrigatorio = true, Minimo = 1, MaxComprimento = 7 }],
                Mapeamentos = [new() { Caminho = "$.nome", Destino = "Empresa.Cidade", Obrigatorio = true }]
            }]
        }
    ];
}
