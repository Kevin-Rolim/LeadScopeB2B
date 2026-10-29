# Entrega — motor dinâmico de integrações REST/JSON

Concluída em 26/10/2026. A aplicação está em execução no Docker Compose, com automação e escrita automática **desabilitadas**. As alterações locais anteriores foram analisadas e preservadas.

## Resultado entregue

O Admin pode cadastrar uma API pela interface, por contrato do catálogo, formulário REST, OpenAPI 3.0/3.1 JSON/YAML ou manifesto privado LeadScopeB2B 1.0. Pode configurar autenticação, operações, parâmetros, corpos JSON, mapeamento, paginação, limites, custo, orçamento e rotinas, sem editar Controllers/Services/Repositories a cada provedor compatível.

Existe um executor, um transporte HTTP, uma autenticação compartilhada, um controle de consumo e um BackgroundService. A CNPJAPI deixou de ser base arquitetural; seu contrato declarativo usa a mesma infraestrutura de ViaCEP, IBGE e APIs privadas/manuais. O cliente/serviço exclusivos e sua configuração legada foram substituídos.

A interface **Integrações e APIs** tem catálogo, cadastro/importação, validação e descoberta de operações, configuração, credenciais, teste local de mapeamento, teste HTTP autorizado, monitoramento e backup. Todas as ações administrativas são protegidas no backend. Testes de execução também verificam o usuário Admin no SQL.

## Funcionalidades efetivamente implementadas

| Área | Implementação |
| --- | --- |
| HTTP | GET, HEAD, OPTIONS, POST, PUT, PATCH e DELETE; JSON, parâmetros tipados, corpo JSON, headers permitidos |
| Autenticação | Nenhuma, API Key em header/query, Bearer, Basic somente HTTPS, OAuth Client Credentials e Authorization Code com PKCE S256; cache/renovação de tokens |
| Importação | OpenAPI 3.0/3.1 JSON/YAML e contrato privado 1.0; referências locais limitadas, versões/recursos incompatíveis informados; operações começam desabilitadas |
| Mapeamento | Caminhos seguros de propriedade/índice, tipos, transforms limitadas, campos obrigatórios, seleção e teste pela interface |
| Paginação | Página, offset e cursor; limite de páginas/itens e detecção de repetição |
| Consumo | Limites separados de administrador/sistema/provedor, requisições, créditos, unidades por item/crédito, custos, mensalidade, orçamento e reservas anteriores ao HTTP |
| Quota | Adaptador declarativo para endpoint de cota e headers oficiais configurados; não presume mecanismo universal de consulta de plano |
| Automação | Worker nativo existente, rotinas por operação, critérios de registros ativos ou sem evidências, parâmetros fixos, cursor, limite, intervalo, política de erro e cancelamento |
| Segurança | HTTPS público; SSRF em validação e socket efetivo; DNS rebinding, redes locais e redirects bloqueados; redes privadas somente por autorização do operador; limites de documento/expansão/corpo/resposta; nenhum código de contrato executado |
| Persistência auxiliar | Arquivos atômicos, lock/semaphore, segredos separados cifrados, revisões, consumo, cooldown e histórico persistentes; backup/restauração documentados e testados |
| SQL acadêmico | Somente persistência compatível de evidências em DadosColetados e auditoria em Logs; revisão humana reutilizada e ampliada para pessoas |

Os testes usam servidores HTTP/TLS locais reais, incluindo autenticação e OAuth, com dados/respostas simulados. Não se afirma certificação dos provedores do catálogo ou validação com contas comerciais reais. Nenhuma chamada comercial externa foi realizada para estes testes.

## Preservação do banco e da arquitetura

- Os 14 models acadêmicos e os scripts definitivos foram comparados com o snapshot inicial e permaneceram idênticos.
- Tabelas, colunas, tipos, nulabilidade, índices e FKs do banco acadêmico foram comparados antes/depois, sem diferenças. Hash SHA-256 da saída ordenada: `f486556db526a3fdec8f296716969c8116f734284ef86c4175c91116b4deb296`.
- Nenhum DDL, migration ou seed foi aplicado no banco acadêmico. Os testes SQL usaram apenas bancos temporários com GUID, excluídos ao final.
- Não houve adição de EF Core, plataformas de automação, worker separado ou containers extras. Compose mantém somente os serviços SQL Server 2022 e ASP.NET Core .NET 10. Os volumes de SQL/chaves foram preservados; `integracoes_data` é o volume auxiliar novo permitido pelo escopo.
- Controller → Service → Repository continua sendo o fluxo de acesso aos dados. O novo repository de arquivos atende exclusivamente à persistência auxiliar explicitamente autorizada, sem criar outro banco.

## Testes efetivamente executados

Total final: **289 verificações** — 106 universais, 13 específicas do motor com SQL, 156 gerais com SQL/MVC/contas e 14 contratos acadêmicos de tabelas. São verificações do runner existente, não uma alegação de 289 testes independentes de framework.

| Validação | Resultado |
| --- | --- |
| Build `dotnet build -c Release --nologo` | Sucesso; zero erros/avisos |
| Runner sem SQL `dotnet run --project Tests -- --universais` durante o desenvolvimento | Sucesso com provedores locais |
| Runner completo `dotnet run --project Tests`, com LEADSCOPE_TEST_CONNECTION isolado | Sucesso com a versão final; bancos temporários removidos |
| A — REST manual sem autenticação | Consulta, validação e mapeamento funcionais |
| B — API autenticada | Bearer, API Key header/query e Basic TLS; isolamento/cifra/mascaramento de credenciais |
| OAuth | Client Credentials, cache, expiração, renovação, Authorization Code PKCE, state vinculado ao usuário/de uso único e refresh token |
| C — OpenAPI | JSON 3.0 e YAML 3.1, operações, parâmetros, autenticação, refs locais, schemas e execução |
| D — API privada | Manifesto versionado, exemplo publicado e exemplo OpenAPI importados/executados sem código C# do provedor |
| E — Limites | 429/Retry-After em segundos/data, cooldown persistente, cotas locais/oficiais, créditos/unidades e bloqueio por custo/orçamento |
| F — Automação | Contrato genérico no BackgroundService, desativação, reserva de ciclo, ausência de duplicação e cancelamento |
| G — Segurança | SSRF IPv4/IPv6, IPs privados/reservados, destinos do operador, DNS rebinding no callback, redirects, especificações malformadas/externas/recursivas/expansivas, YAML aliases/tags e JSONPath com código |
| Robustez | HTTP 400/401/403/404/503, timeout, cancelamento, limite de resposta, JSON inválido, paginação, retries limitados e escrita sem replay automático |
| SQL | Persistência/deduplicação, ausência de sobrescrita automática, destino ausente, aprovação humana, identidade concorrente, revisão de pessoa e critérios de atualização de evidências |
| MVC | Cadastro REST com campos opcionais vazios, configuração, mapeamento e execução de ponta a ponta pela interface; restrição de acesso e CSRF |
| Persistência | Reinício, lock de escritor, arquivo inválido sem reset, versão concorrente, backup e restauração de contrato/segredo/chaves |
| Docker | Build/publicação Release, início da aplicação com automação desabilitada e SQL saudável |
| HTTP no Compose | Login/CSS/JS 200; gerenciador protegido 302 sem sessão; API de entrada 401 sem chave |
| Metadados acadêmicos antes/depois | Idênticos; 14 tabelas e zero bancos temporários restantes |
| `git diff --check` | Sem erros |

A validação especial de certificado de fixture fica somente no projeto Tests, excluído do binário de produção. A aplicação usa validação normal de TLS. SMTP nos testes de contas é também local; não foram enviados e-mails a terceiros.

## Arquivos criados

- `Controllers/IntegracoesApisController.cs`.
- `Integration/Universal/Contratos.cs`, `ValidacaoContrato.cs`, `ImportadorApi.cs`, `DestinosHttp.cs`, `ApiHttpService.cs`, `MapeadorApi.cs`, `CatalogoApis.cs`.
- `Repositories/IntegracaoArquivoRepository.cs` e `IntegracaoRepository.Universal.cs`.
- `Services/AutenticacaoIntegracaoService.cs`, `ConsumoIntegracaoService.cs`, `ExecutorIntegracaoService.cs`, `GerenciadorIntegracaoService.cs`, `AutomacaoIntegracaoService.cs`.
- `ViewModel/Integracoes/ApiForms.cs`.
- `Views/IntegracoesApis/Index.cshtml`, `Catalogo.cshtml`, `Nova.cshtml`, `Importar.cshtml`, `Configurar.cshtml`, `Operacao.cshtml`, `Executar.cshtml`, `_Parametros.cshtml`, `_Mapeamentos.cshtml`.
- `wwwroot/js/integracoes-apis.js`.
- `Tests/ApiTestServer.cs` e `UniversalApiTests.cs`.
- `Docs/MotorIntegracoes.md`, `ContratoIntegracaoLeadScopeB2B.md`, `EntregaMotorIntegracoes.md`.
- `Docs/Exemplos/contrato-privado-1.0.json`, `openapi-privada-3.1.yaml`, `api_privada.py`.

## Arquivos modificados

- `Program.cs`, `LeadScopeB2B.csproj` (YamlDotNet e exclusão do armazenamento privado da publicação), `appsettings.json`, `compose.yaml`, `.dockerignore`, `.gitignore`.
- `Services/ColetaAutomaticaWorker.cs`: agora executa operações declarativas sem referência a provedor.
- `Repositories/IntegracaoRepository.Coleta.cs`: preserva helpers/revisão existentes e aceita os campos permitidos de pessoas/empresas.
- `Security/Erros.cs`: classificação segura de erros do motor.
- `Views/Integracoes/Index.cshtml`, `Revisar.cshtml` e `Views/Shared/_Layout.cshtml`.
- `Tests/Program.cs` e `ContaFlowTests.cs`: suíte universal, interface e isolamento da automação/configuração.
- `README.md`, `Docs/Integracoes.md`, `ColetaAutomatica.md` e `EntregaColetaAutomatica.md`: documentação atual e identificação do relatório antigo como histórico.

## Implementações exclusivas substituídas

`Integration/CnpjApiClient.cs`, `Integration/ColetaAutomaticaOptions.cs`, `Services/ColetaCnpjService.cs` e `Tests/ColetaCnpjTests.cs` foram substituídos pelos mecanismos/testes universais. Esses arquivos existiam localmente no início desta tarefa. As evidências e auditoria da primeira integração não foram removidas.

## Configuração e operação

Consulte [o guia completo](MotorIntegracoes.md) e [o contrato privado](ContratoIntegracaoLeadScopeB2B.md). O administrador começa em **Integrações e APIs**, escolhe uma das três formas de cadastro, configura credencial/plano/operação/mapeamento e testa antes de habilitar rotinas. Segredos nunca entram em contratos ou arquivos versionados.

No Compose, `COLETA_HABILITADA` e `INTEGRACOES_ESCRITA_AUTOMATICA` continuam false por padrão. OAuth exige URL pública HTTPS de callback. APIs internas exigem a autorização de origens/CIDRs pelo operador, configurável em `INTEGRACOES_REDES_PRIVADAS_JSON`. Backup administrativo e restauração com a aplicação parada e as chaves originais estão descritos no guia e têm testes.

## Limitações e pendências explícitas

- Não são implementados todos os recursos OpenAPI ou todos os protocolos possíveis. SOAP, GraphQL, webhooks, assinaturas proprietárias, formatos especiais e precificação não representável exigem adaptadores/versão compatível. Não existem conectores fictícios para essas funcionalidades.
- Mapeamento de vínculos/CNAEs funciona em prévia/resultado interno; persistência automática nesses contextos é bloqueada porque DadosColetados não possui as FKs correspondentes. Não são criados registros/relacionamentos fictícios.
- Escrita externa depende de autorização explícita; não há garantia distribuída de exatamente uma aplicação de uma operação. Cancelamento após envio pode deixar resultado externo incerto; não repetir sem conferir.
- O armazenamento suporta uma instância local, até 20 integrações, contratos internos de 256 KiB, histórico de 2.000 eventos e estado de 32 MiB. Locks/rename precisam ser suportados pelo filesystem. Não há coordenação multi-instância.
- Reservas são estimativas conservadoras, não fatura. Consumo externo à aplicação não é conhecido. Informações de preço/cota desconhecidas não são inventadas. A restauração de backup antigo exige reconciliar consumo com o fornecedor.
- A configuração CNPJAPI antiga precisa ser recadastrada explicitamente no catálogo/armazenamento cifrado. A consulta real CNPJAPI e testes de OAuth com contas de produção continuam dependentes de credenciais e autorização do fornecedor; não foram simulados como validação comercial real.
