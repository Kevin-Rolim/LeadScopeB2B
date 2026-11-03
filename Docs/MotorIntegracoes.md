# Integrações e APIs — guia operacional

O módulo conecta APIs REST/JSON por contratos declarativos, com configuração pela interface. O mesmo executor atende catálogo, OpenAPI, REST manual, contratos privados e rotinas. Não existe cliente HTTP, autenticação, controle de consumo ou agendador próprio de cada provedor.

O projeto continua ASP.NET Core MVC/.NET 10, ADO.NET e SQL Server 2022. O caminho Controller → Service → Repository foi preservado. Nenhum script SQL, model acadêmico, tabela, coluna, índice ou relacionamento foi modificado por esta evolução. As 14 tabelas existentes continuam sendo o limite da persistência de negócio.

Consulte também o [guia da conexão simplificada e descoberta automática](SimplificacaoIntegracoes.md). As configurações técnicas permanecem em **Opções avançadas**.

## Primeiro acesso

Entre com uma conta Admin e abra **Integrações e APIs**. Gerenciar configurações, segredos, testes, contratos, backup e OAuth exige Admin no backend. A revisão de evidências existente continua disponível a Admin/Operação. Além da autorização MVC, cada execução do motor revalida no SQL que o usuário responsável ainda é Admin.

A página inicial mostra integrações, operações habilitadas, consumo mensal, reserva financeira, último resultado e eventos recentes. Uma integração nova não executa HTTP ao ser cadastrada. Integração, operação, revisão e autorização de uso do plano têm controles separados.

## Selecionar uma API do catálogo

1. Abra **Catálogo** e adicione a API desejada.
2. Informe somente a credencial exigida; URL e autenticação vêm do catálogo.
3. Selecione as operações e habilite a integração. Quando disponível, a consulta gratuita de cota verifica a conexão automaticamente.
4. Ative a automação se desejar. Custos desconhecidos ou operações cobradas exigem autorização explícita. Parâmetros indispensáveis são solicitados quando não podem vir dos cadastros.

Plano, mensalidade e orçamento não são obrigatórios. Fonte de evidências compatível é preparada automaticamente; opções técnicas e limites manuais continuam disponíveis em **Opções avançadas**.

Catálogo atual:

| Contrato | Operações | Autenticação | Situação |
| --- | --- | --- | --- |
| CNPJAPI | Empresa por CNPJ; consulta de cota | Bearer | Contrato baseado na documentação e validado com resposta simulada; consulta real depende da conta/plano |
| ViaCEP | CEP → cidade/UF | Nenhuma | Contrato baseado na documentação; sem promessa de volume ilimitado |
| IBGE Localidades | Município por código | Nenhuma | Contrato baseado na documentação; custo/condições permanecem sujeitos a confirmação |

Esses contratos não significam parceria, certificação ou integração oficial reconhecida pelo fornecedor. Não há cartões de CRM/comunicação fingindo conectores implementados: outras APIs compatíveis podem ser cadastradas manualmente ou importadas.

Documentação consultada: [CNPJAPI](https://cnpjapi.com.br/docs), [ViaCEP](https://viacep.com.br/) e [IBGE](https://servicodados.ibge.gov.br/api/docs/localidades). A documentação CNPJAPI descreve chave Bearer, cotas e rate limits por plano; não se utiliza sua consulta web anônima. Sua operação de cota é um adaptador **declarativo**, com caminhos `$.rpm` e `$.restante`, interpretado pelo mecanismo genérico.

## Importar OpenAPI

1. Abra **Importar OpenAPI ou contrato privado**.
2. Envie arquivo JSON/YAML ou cole o documento. Máximo: 1 MiB.
3. Escolha o perfil de rede já autorizado no servidor. O cadastro não pode criar perfis privados.
4. Use **Validar e descobrir operações** para conferir compatibilidade. Depois importe desabilitada.
5. Revise as operações e complete mapeamento, limites, custos, credenciais e escopos. Faça uma prévia local antes de habilitar.

São suportadas versões **3.0.x e 3.1.x**, servidores absolutos (primeiro servidor selecionado), variáveis de servidor com default, métodos convencionais, parâmetros de rota/query/header, corpo `application/json`, schemas simples de resposta, autenticações suportadas e referências **locais** `#/...` sem recursão. Referências locais são expandidas com limite de profundidade.

A importação respeita segurança por operação, inclusive operação sem autenticação. Alternativas OR selecionam uma alternativa suportada, informada para revisão. Combinações AND de múltiplos esquemas não são executadas. Escopos OAuth descobertos ficam nas permissões da operação e na configuração de autenticação.

Limitações explícitas: OpenAPI 2.x/3.2, referências externas ou recursivas, cookies de autenticação, `content` em parâmetros, serialização complexa/explode, autenticação proprietária, servidor por operação, multipart, formulário de negócio e composição de schemas exigem revisão manual ou adaptador. Recursos incompatíveis não são silenciosamente habilitados. Uma operação incompatível não pode ser ativada; pode-se criar uma operação manual equivalente dentro do subconjunto suportado.

O parser YAML utiliza YamlDotNet, sem deserialização de tipos CLR indicados pelo documento. Anchors, aliases, tags customizadas e múltiplos documentos são recusados. Não há download de `$ref`, leitura de `file://` ou avaliação de expressões. O documento tem limite de 48 níveis e 30 mil eventos YAML; a expansão de referências OpenAPI também é limitada a 30 mil nós e 2 milhões de caracteres; cada integração admite até 100 operações e cada operação até 100 parâmetros/mapas.

## Configurar REST manualmente

Em **Configurar REST manualmente**, informe URL base, método/endpoint e autenticação necessária; nome e opções adicionais são opcionais. Defina método/endpoint e adicione parâmetros pelo formulário:

- `path`: placeholder como `/companies/{id}`; obrigatório, substituído com escaping de segmento.
- `query`: parâmetros codificados na query string.
- `header`: headers convencionais; Host, cookies, Authorization e headers de transporte ficam reservados. Autenticação tem seção própria.
- `body`: propriedades JSON tipadas; o executor monta o objeto sem exigir scripts. Arrays/objetos podem ser informados em JSON.

Tipos: string, integer, number, boolean, object e array; obrigatório, tamanho máximo, mínimo/máximo numéricos e enumerações importadas são validados. Há formatos CNPJ, CNPJ numérico, e-mail e URI. Campos do cadastro podem preencher parâmetros, como `Empresa.Cnpj` e `Pessoa.Email`.

Depois do cadastro, edite a operação para configurar paginação, mapeamento, custos e rotina. É possível adicionar novas operações e métodos de autenticação à mesma integração. O corpo fixo JSON e schema de resposta são configurações declarativas avançadas, opcionais. Nomes de parâmetros/caminhos de mapeamento de senha, token e credencial são recusados; não use parâmetros comuns como armazenamento de autenticação.

Métodos executados: **GET, HEAD, OPTIONS, POST, PUT, PATCH e DELETE**. HEAD e HTTP 204 podem terminar sem JSON. Outros retornos precisam conter JSON válido. XML, SOAP, GraphQL, webhooks, streaming, multipart e assinaturas proprietárias não têm implementação genérica nesta versão. O contrato não executa JavaScript, Python, templates de código ou tipos externos.

## Autenticação

| Tipo no contrato | Campos protegidos | Comportamento |
| --- | --- | --- |
| `none` ou autenticação vazia | Nenhum | Requisição sem credenciais |
| `apiKeyHeader` | Valor | Header configurado no contrato |
| `apiKeyQuery` | Valor | Somente quando exigido pelo fornecedor; query não é registrada nos logs do cliente |
| `bearer` | Valor | Authorization: Bearer |
| `basic` | Usuário e senha | Exclusivamente HTTPS, inclusive em perfis privados |
| `clientCredentials` | Client ID e client secret | OAuth 2.0, token endpoint HTTPS; cliente por Basic ou corpo |
| `authorizationCode` | Client ID; client secret se cliente confidencial | OAuth 2.0 com PKCE S256, estado de uso único vinculado ao Admin, validade de 10 minutos e refresh token quando disponível |

Segredos são criptografados por ASP.NET Core Data Protection, com propósito por integração **e** autenticação. Arquivos separados não permitem transplantar uma credencial para outra integração. Não são devolvidos à interface, ao exportar contratos ou ao histórico. Campos vazios ao salvar preservam valores existentes; a atualização invalida tokens em cache. Remover apaga a credencial ativa local, mas não revoga a credencial no fornecedor nem elimina backups criptografados.

OAuth Authorization Code exige `MotorIntegracoes:UrlPublicaOAuth` no servidor, por exemplo `https://leadscope.empresa.com`. Cadastre no provedor o callback **exato** `https://leadscope.empresa.com/IntegracoesApis/OAuthCallback`. O callback exige a sessão Admin que iniciou a conexão. Tokens vencidos são renovados; refresh tokens rotacionados são persistidos. Um 401 invalida o token em cache para nova autenticação na próxima execução; não repete automaticamente a operação que falhou. Falta de refresh token exige reconectar a conta.

As trocas OAuth também usam transporte, rede, timeout, orçamento e rate limit compartilhados. Cada troca conta como requisição e reserva conservadoramente o custo da operação associada. Portanto, reserve margem para token + consulta, ou configure custos gratuitos apenas quando conhecidos. O botão de conexão informa e recebe autorização de custo quando necessária. Endpoints OAuth devem ser HTTPS sem query fixa; extensões proprietárias como assinaturas e parâmetros de grant adicionais exigem adaptação.

Códigos distintos identificam credencial ausente/inválida, token expirado/invalidado, permissão insuficiente, rate limit e indisponibilidade. Erros do provedor não são registrados com seu corpo bruto. Arquivos criptografados inválidos são informados e podem ser recuperados pelas chaves originais ou por recadastro explícito da credencial.

## Mapeamento e persistência de negócio

Os caminhos aceitam seleção como `$.company.name`, `$.razao_social`, `$[0].name` e `$["nome com espaço"]`. Não há filtros, scripts, recursão ou curingas. **Caminho dos itens** seleciona um objeto ou um array; cada mapa é relativo a um item.

Transformações permitidas: trim, upper, lower, digits, cnpj, url e none. Contratos podem declarar `camposOmitidosAmostra`; o contrato CNPJAPI omite QSA também da amostra visual. O resultado passa pela normalização e pelos limites dos campos existentes. Campos obrigatórios incompatíveis recusam a execução; campos opcionais inválidos são descartados com aviso. A resposta pode ter schema simples: objetos/propriedades obrigatórias, tipos, arrays/itens, enum, intervalos e tamanhos. Formatos comuns são verificados. Regex e composição `oneOf/anyOf/allOf`, entre outros recursos não suportados, geram incompatibilidade explícita.

No editor, cole uma amostra JSON e use **Listar campos e testar mapeamento**: não faz HTTP, mostra caminhos disponíveis e valores normalizados. Esses caminhos ficam disponíveis na seleção de campo. No teste real, o motor retorna uma amostra limitada, mascarando segredos conhecidos, valores codificados de autenticação e campos sensíveis. Corpo bruto e amostra não são mantidos no histórico operacional.

| Destino lógico | Utilização |
| --- | --- |
| `Empresa.*` | Prévia e evidências em DadosColetados para EmpresaId existente |
| `Pessoa.*` | Prévia e evidências em DadosColetados para PessoaId existente e disponível |
| `Vinculo.Cargo/Departamento/Senioridade` | Prévia normalizada, disponível ao chamador interno; não cria vínculo/relacionamento automaticamente |
| `Cnae.Numero/Descricao` | Prévia normalizada, disponível ao chamador interno; não insere CNAEs nem altera relações automaticamente |

A tabela `DadosColetados` exige exatamente um destino pessoa/empresa e não possui FK para CNAE ou par pessoa–empresa. Por isso, evidências de vínculo/CNAE não são forçadas nessa tabela. Use os módulos próprios e sua revisão para alterações desses cadastros; adaptação específica será necessária para automatizar propostas com esse contexto adicional.

Para gravar evidências, escolha uma fonte ativa e **exatamente um registro existente**, com resposta única, um item e sem paginação. O executor não cria empresas, pessoas ou relacionamentos. O lote grava `Campo`, `Valor`, `FonteDadosId` e o destino compatível, com confiança nula, sob transação e comparação de duplicidade. Uma mudança do CNPJ durante o HTTP descarta a resposta para aquele destino.

A identidade da resposta pode ser comparada a um parâmetro por `correspondencia`. O contrato CNPJAPI e o exemplo privado fazem essa comparação. Sem tal declaração, o sistema não inventa uma chave externa. O administrador deve revisar a correspondência adequada antes de habilitar enriquecimento.

Os dados ficam em **Dados coletados**, com origem identificada. Campos de pessoa e campos empresariais permitidos podem ser aprovados/rejeitados, com verificação de concorrência e decisão em `Logs`. Não há sobrescrita automática nem mudança automática do status validado. O CNPJ externo permanece referência de identidade, sem botão para alterar o identificador. `Revisoes` continua reservada aos vínculos pessoa–empresa do esquema original.

## Paginação, falhas e segurança

Paginação por página, offset ou cursor configura nome do parâmetro, início, tamanho, parâmetro de tamanho opcional, caminho de próximo cursor e máximo de páginas. Cursor é valor codificado de parâmetro, nunca URL a seguir. Limites de páginas (até 100) e de itens por execução (1.000), detecção de cursor/página repetidos e cancelamento evitam ciclos infinitos. Evidências SQL paginadas são bloqueadas para evitar associação ambígua com um único cadastro.

Timeout: 1–120 segundos por chamada, incluindo leitura; requisição: até 256 KiB; resposta: até 1 MiB; headers: até 32 KiB; JSON: 32 níveis. O HTTP usa `IHttpClientFactory`. Redirects, cookies e proxies automáticos são desabilitados. Uma nova conexão HTTP/1.1 é validada para cada requisição, evitando reutilizar sockets entre perfis de rede diferentes.

Retries extras (0–2) são opcionais para 502/503/504 em métodos de leitura, sem repetir escritas. `Retry-After` interrompe novas chamadas e é persistido, aceitando segundos ou data HTTP. Na ausência de cabeçalho, 429/503 recebem espera local de um minuto. Uma chamada rejeitada conta no consumo; timeout/cancelamento após reserva também contam conservadoramente. Não há retry indefinido ou tentativa de contornar proteção.

**SSRF:** APIs públicas exigem HTTPS e endereços públicos. São bloqueados localhost, loopback, endereços privados, link-local, metadata, IPv4 mapeado em IPv6 e faixas reservadas. Todos os endereços DNS precisam ser permitidos. O DNS é resolvido novamente no `ConnectCallback`, e o socket conecta diretamente ao IP validado, sem uma terceira resolução implícita. TLS usa a validação normal de certificado; não há opção administrativa na interface para desabilitá-la.

## Autorizar uma API interna

Somente o operador do servidor pode criar perfis em `MotorIntegracoes:RedesPrivadas`, por configuração protegida/variáveis de ambiente. A interface apenas seleciona um perfil já existente. Cada perfil precisa de origens exatas (scheme, host e porta) e CIDRs permitidos. HTTP é permitido somente quando o operador o habilita explicitamente; Basic e OAuth continuam exigindo HTTPS.

Exemplo local para `dotnet run` no host, junto da API de demonstração:

```bash
export MotorIntegracoes__RedesPrivadasJson='[{"id":"demo-local","origens":["http://127.0.0.1:8015"],"redes":["127.0.0.1/32"],"permitirHttp":true}]'
```

No Compose, a mesma estrutura é recebida pela variável do operador `INTEGRACOES_REDES_PRIVADAS_JSON` (ou listas via configuração .NET). Exemplo corporativo:

```json
[{"id":"erp-interno","origens":["https://erp.interna.example:8443"],"redes":["10.50.20.8/32"],"permitirHttp":false}]
```

Origem e IP resolvido precisam ser permitidos simultaneamente. A regra não autoriza outros serviços do host ou de toda a rede. Certificados internos devem ser confiáveis no sistema operacional do container; não se ignora TLS. `127.0.0.1` dentro do container aponta ao próprio container: para uma API corporativa externa, informe seu DNS/IP real e a autorização correspondente. O exemplo Python em loopback destina-se à execução local no host, não à criação de um terceiro container.

## Consumo, limites e orçamento

A descoberta prioriza endpoint oficial configurado, headers com semântica reconhecida, metadados declarados no contrato/catálogo e proteção local. A interface distingue limites confirmados, documentados, desconhecidos e locais. Consulte os detalhes e limitações no [guia da simplificação](SimplificacaoIntegracoes.md).

Não se exige plano, mensalidade, moeda ou orçamento para conectar. Custos desconhecidos são exibidos como desconhecidos; consultas manuais e automação exigem autorizações próprias. Um teto financeiro é opcional e avançado. Se ele estiver ativo e o custo for desconhecido, é necessária uma reserva conservadora explícita; a reserva não representa a fatura real.

## Automatizar

O `ColetaAutomaticaWorker` existente foi reaproveitado, com `PeriodicTimer` e `IServiceScopeFactory`. Não conhece CNPJAPI ou outro fornecedor. Consulta rotinas declaradas e usa o mesmo `IExecutorApi` de execução manual.

1. Configure e teste integração/operação.
2. No editor, informe Admin responsável, intervalo, limite de registros e critério: parâmetros fixos, empresas/pessoas sem evidências da fonte, ou empresas/pessoas ativas (incluindo registros já enriquecidos).
3. Indique origem dos parâmetros no cadastro, persistência de evidências e política de erro. Nunca tente criar destinos fictícios.
4. Ative a automação na tela principal da integração. Essa ação autoriza explicitamente suas rotinas sem configuração adicional no servidor. Rotinas antigas criadas somente no editor continuam dependendo de `MotorIntegracoes:AutomacaoHabilitada=true` (`COLETA_HABILITADA=true` no Compose).

Os critérios de registros ativos permitem atualizar evidências periodicamente; respostas iguais não duplicam linhas SQL, mas cada nova consulta consome cota e requer as autorizações de consumo aplicáveis. O critério sem evidências considera a FonteDados inteira: integrações/operações que compartilham uma fonte também compartilham esse filtro.

O agendador reserva a próxima execução persistentemente antes de executar, percorre registros com cursor para não bloquear indefinidamente os demais em falhas e respeita cooldown. Uma execução aguardada por vez, compartilhada com consultas manuais, sem filas externas. Escrita automática também exige autorização específica na operação, na rotina e `MotorIntegracoes:PermitirEscritaAutomatica` do servidor; o padrão é false. Leituras e escritas não têm garantia distribuída de exatamente uma chamada. Chamadas de escrita interrompidas podem ter sido aplicadas no fornecedor; confira o histórico e o destino antes de repetir.

## Persistência auxiliar, backup e restauração

Configuração padrão local: `App_Data/Integracoes`, fora da raiz pública e do Git/publicação. Compose: `/app/integracoes`, no volume `integracoes_data`. O armazenamento contém:

- `configuracao.json`: contratos, revisões atuais/anteriores (últimas 20 alterações), consumo agregado, cooldown, agendas e últimos 2.000 eventos sem corpos, parâmetros de execução ou tokens.
- `segredo-<integracao>-<auth>.bin`: credenciais/tokens/estado PKCE criptografados.
- `.bak`: versão anterior de cada arquivo substituído; `.process.lock`: exclusão de escritor.

São usados semáforo no processo, lock de arquivo mantido durante sua vida e substituição atômica por arquivo, com flush em disco e permissões privadas. O arquivo público tem limite de 32 MiB; até 20 integrações e 256 KiB por contrato interno. Configuração corrompida ou ausente com backup/segredos não é silenciosamente substituída por estado vazio. Custos/créditos agregados sobrevivem à retenção do histórico. Credenciais cifradas não ficam seguras contra alguém que controle o host e tenha também as chaves de proteção; proteja ambos os volumes.

**Backup:** o botão administrativo baixa ZIP consistente dos contratos e segredos já cifrados. Guarde-o em local protegido. Faça também backup independente do volume `dataprotection_keys` e do banco SQL pelo procedimento existente. O ZIP não contém as chaves mestras, nem substitui backup do banco.

**Restauração pelo operador:**

1. Pare somente `leadscope`; preserve SQL Server e todos os volumes. Não remova volumes.
2. Faça uma cópia protegida do diretório atual para poder reverter.
3. Restaure as chaves originais de Data Protection e os arquivos `configuracao.json`/`segredo-*.bin` do backup confiável no volume de integrações. Extraia apenas nomes simples esperados, sem caminhos externos. Não importe ZIPs desconhecidos.
4. Mantenha a propriedade/permissões privadas dos arquivos. Preserve o mesmo ApplicationName `LeadScopeB2B`.
5. Antes de iniciar, mantenha desabilitadas as integrações/rotinas restauradas, inclusive `AutomacaoPelaInterface`; `COLETA_HABILITADA=false` controla rotinas antigas e não revoga autorizações explícitas pela interface. Confira contratos, consumo, cooldown e credenciais antes de reativar.
6. Se o problema for somente o JSON principal, restaure sua `.bak` após avaliar a defasagem dos contadores; um backup antigo pode subestimar consumo. Confirme o consumo real no fornecedor antes de liberar consultas.

Backup/restauração e reinício são exercitados pelos testes. Não há transação única entre arquivos e SQL: se o SQL gravou evidências e a gravação do resumo operacional falhar, as evidências permanecem e a deduplicação impede reinserção. Reservas anteriores ao HTTP são conservadoras para falhas/incertezas. Uma falha de energia durante a substituição de vários arquivos em uma restauração exige concluir/reverter a restauração com a aplicação parada.

Esta versão requer **uma instância**, armazenamento local com suporte às semânticas de lock/rename e espaço disponível. Não use múltiplas réplicas ou compartilhamento de rede sem garantias equivalentes. Custos não são contabilidade financeira; dados externos não são validados como verdade automaticamente.

## Executar com Docker Compose

Permanecem somente `sqlserver` (SQL Server 2022) e `leadscope` (.NET 10). Os volumes anteriores de banco/chaves foram preservados; o único volume novo é o armazenamento auxiliar permitido para integrações.

```bash
docker compose build leadscope
COLETA_HABILITADA=false docker compose up -d leadscope
```

Variáveis do servidor: `COLETA_HABILITADA` (false), `INTEGRACOES_ESCRITA_AUTOMATICA` (false), `INTEGRACOES_URL_PUBLICA_OAUTH` e `INTEGRACOES_REDES_PRIVADAS_JSON`. Credenciais dos provedores são cadastradas pela interface e persistidas cifradas, sem variáveis específicas de cada fornecedor no Compose. Conexão SQL, portas e volumes anteriores não foram alterados. Não é necessário recriar banco ou executar script SQL.

## Compatibilidade com a coleta anterior

Os dados/evidências e decisões SQL existentes permanecem disponíveis. O cliente, orçamento e serviço exclusivos da CNPJAPI foram substituídos pelo executor/consumo universais; não há duas automações paralelas. As antigas opções `ColetaAutomatica:*` e `CNPJAPI_*` não são migradas silenciosamente. Para continuar, adicione CNPJAPI pelo catálogo, recadastre a chave na persistência protegida e selecione operações/automação. O padrão continua sem consultas automáticas.

As APIs **de entrada** `/api/...`, autenticadas com X-Api-Key, foram preservadas e são independentes das credenciais externas do motor. A revisão de pessoas/empresas foi ampliada reutilizando o mesmo fluxo de evidências e `Logs`.

## Testes e exemplos

```bash
dotnet build -c Release
dotnet run --project Tests -- --universais
# LEADSCOPE_TEST_CONNECTION deve apontar a um SQL de teste com permissão CREATE DATABASE:
dotnet run --project Tests
```

O runner existente agora inclui provedores HTTP/TLS locais reais, com contratos diferentes, respostas simuladas e nenhuma chamada desnecessária a provedores comerciais. A suíte completa usa bancos temporários `LeadScopeB2B_Tests_<guid>` e os exclui. Processos MVC usam armazenamento isolado e automação explicitamente desabilitada. A validação de certificado especial dos fixtures existe somente em Tests; a aplicação mantém TLS normal.

Veja [como uma empresa fornece sua API](ContratoIntegracaoLeadScopeB2B.md), o [manifesto privado](Exemplos/contrato-privado-1.0.json), a [especificação OpenAPI](Exemplos/openapi-privada-3.1.yaml) e a [API de demonstração](Exemplos/api_privada.py). Pontos de extensão: `IImportadorApi`, `IExecutorApi`, `IAutenticacaoApi`, `IMapeadorApi`, `IConsumoApi` e `ICatalogoApis`. Adaptadores são código revisado do projeto; contratos não carregam bibliotecas ou código fornecido por usuários.
