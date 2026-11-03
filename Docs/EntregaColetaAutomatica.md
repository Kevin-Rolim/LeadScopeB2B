> Relatório histórico da primeira integração. A implementação atual está em [EntregaMotorIntegracoes.md](EntregaMotorIntegracoes.md); a configuração atual está em [MotorIntegracoes.md](MotorIntegracoes.md).

# Entrega — coleta automática CNPJAPI

Implementação concluída e validada em 26/10/2026. A coleta permanece **desabilitada**, inclusive na aplicação em execução no Docker Compose. As alterações locais anteriores foram examinadas e preservadas. Não houve alteração de esquema ou execução de script de banco no ambiente acadêmico.

## Arquivos desta entrega

Criados:

- `Integration/CnpjApiClient.cs`: consulta autenticada, timeout, HTTP/Retry-After e interpretação seletiva do JSON.
- `Integration/ColetaAutomaticaOptions.cs`: configuração e validação dos limites.
- `Integration/RevisaoColeta.cs`: dados para comparação e revisão.
- `Services/ColetaCnpjService.cs`: seleção, orçamento, consulta e encaminhamento das evidências.
- `Services/ColetaAutomaticaWorker.cs`: BackgroundService com escopos, PeriodicTimer e cancelamento.
- `Repositories/IntegracaoRepository.Coleta.cs`: extensão do repository existente para coleta e revisão ADO.NET, sem novas tabelas.
- `Views/Integracoes/Revisar.cshtml`: comparação, aprovação e rejeição humanas.
- `Tests/ColetaCnpjTests.cs`: respostas simuladas, worker e cenários SQL isolados.
- `Docs/ColetaAutomatica.md`: configuração, operação, fonte, limites e decisões.
- `Docs/EntregaColetaAutomatica.md`: este relatório.

Modificados:

- `Program.cs`: registro do cliente HTTP, configuração, serviço e worker.
- `appsettings.json`: valores iniciais desabilitados, sem credenciais.
- `compose.yaml`: variáveis de ativação/configuração; mesmos serviços, conexão e volumes. O arquivo já existia localmente, sem rastreamento, antes desta entrega.
- `Repositories/IntegracaoRepository.cs`: deduplicação também na entrada de evidências existente.
- `Services/IntegracaoService.cs`: identificação da fonte e acesso ao fluxo de revisão.
- `Controllers/IntegracoesController.cs`: revisão MVC autorizada e protegida por antiforgery.
- `Views/Integracoes/Index.cshtml`: nome da origem e acesso à comparação.
- `Repositories/EmpresaRepository.cs`: correção pontual do SQL de edição, que ainda continha `Cnpj=@cnpj=LEFT(@cnpj,8)` nas alterações locais; agora usa somente `Cnpj=@cnpj`, compatível com o banco definitivo.
- `Tests/Program.cs`: cenários de coleta, revisão MVC e automação explicitamente desabilitada nos processos de teste.
- `Tests/ContaFlowTests.cs`: automação explicitamente desabilitada nos processos da suíte de contas.
- `README.md` e `Docs/Integracoes.md`: descrição atualizada e referências à configuração.

Nenhum model, script SQL, migration, índice ou relacionamento foi criado/modificado por esta implementação. Os demais arquivos já modificados no repositório pertenciam ao estado local anterior.

## Fonte e funcionamento

Fonte: [CNPJAPI](https://cnpjapi.com.br/docs/consulta-cnpj), `GET https://api.cnpjapi.com.br/{cnpj}`, com chave Bearer. A API admite uso programático autenticado, sujeito aos [limites do plano](https://cnpjapi.com.br/docs/rate-limit) e às [condições comerciais](https://cnpjapi.com.br/termos). Não é uma API ilimitada.

O worker executa sequencialmente dentro do processo MVC. Seleciona empresas existentes com CNPJ válido e sem evidências da fonte. Revalida destino, fonte e usuário, reserva orçamento em Logs, chama a API uma vez por empresa no ciclo e persiste um lote transacional. Respeita intervalo, limite por execução, timeout, cancelamento, erros HTTP e Retry-After. Erros individuais recuperáveis não encerram permanentemente a automação.

Armazena CNPJ de referência, razão social, nome fantasia, porte, cidade e UF em `DadosColetados`, com `EmpresaId` e `FonteDadosId`. Não armazena QSA, contatos, JSON bruto nem confiança presumida. Registra comparação e auditoria em `Logs`. Nenhuma empresa é criada ou atualizada pela coleta. A atualização dos cinco campos revisáveis exige aprovação humana explícita na interface. Rejeição mantém o cadastro intacto; decisões duplicadas e aplicação após mudança do CNPJ são bloqueadas.

## Configuração necessária

Veja [ColetaAutomatica.md](ColetaAutomatica.md) para as chaves completas, variáveis Compose e exemplos. São obrigatórios:

- Fonte ativa `CNPJAPI`, tipo `API`, URL `https://api.cnpjapi.com.br`.
- Usuário existente de Admin/Gestor responsável pela auditoria.
- Chave externa em User Secrets ou variável de ambiente.
- Orçamentos RPM/diário/mensal positivos, reservados para esta aplicação e cobertos pelo plano.
- `UsoAutorizado=true` e `Habilitada=true` explicitamente configurados.

No Compose, a ativação usa `COLETA_HABILITADA`; por padrão é `false`. Não foi gravada credencial em arquivo versionado.

## Validação executada

| Verificação | Resultado |
| --- | --- |
| `dotnet build -c Release --nologo` | Sucesso, zero avisos e erros |
| `dotnet run --project Tests -- --coleta` | Sucesso com respostas simuladas e contrato das tabelas |
| `dotnet run --project Tests`, usando `LEADSCOPE_TEST_CONNECTION` para o SQL Server 2022 local | Sucesso: 146 verificações gerais, 46 específicas de coleta e 14 contratos de tabelas |
| Fluxo completo SQL → serviço → HTTP simulado → JSON → evidências SQL | Sucesso |
| CNPJ válido/inválido; HTTP 200, 400, 401, 403, 404, 429, 503; Retry-After em segundos/data | Sucesso |
| Timeout, cancelamento HTTP/SQL/worker, dados parciais/inválidos, duplicidades, limites, rollback, ausência de atualização automática | Sucesso |
| Revisão manual em SQL/MVC, autorização, CSRF, decisão repetida, valor concorrente e mudança do CNPJ | Sucesso |
| Suíte de contas/autenticação existente com automação desabilitada | Sucesso |
| `docker compose build leadscope` | Sucesso com publicação Release .NET 10 |
| `COLETA_HABILITADA=false docker compose up -d --no-deps leadscope` | Aplicação recriada e iniciada; SQL Server existente preservado |
| HTTP no Compose: login e CSS | 200 / 200 |
| HTTP no Compose: empresas sem sessão e API sem chave | 302 / 401, conforme esperado |
| Log do worker no Compose | “Coleta automática desabilitada.” |
| Comparação de metadados do banco acadêmico antes/depois | Idênticos; 14 tabelas, colunas, índices e FKs preservados |
| Bancos `LeadScopeB2B_Tests_*` restantes após a suíte | Zero |
| `git diff --check` | Sem erros |

As verificações SQL criaram e excluíram somente bancos temporários com GUID. O banco acadêmico foi consultado para inspeção de metadados; nenhum DDL foi aplicado nele. O hash SHA-256 da saída ordenada de metadados de tabelas/colunas/tipos, índices e FKs permaneceu `f486556db526a3fdec8f296716969c8116f734284ef86c4175c91116b4deb296`.

## Pendências e limites

A consulta real controlada **não foi executada**: não havia chave CNPJAPI no ambiente, no `.env` nem em User Secrets, nem plano autorizado configurado. Após a configuração apropriada, pode-se validar com `LimitePorExecucao=1`, orçamento conservador e uma empresa existente sem evidências; conferir Dados coletados e manter qualquer alteração cadastral sujeita à revisão humana.

A versão opera com uma única instância, sem coordenação distribuída. Os orçamentos persistentes contam tentativas em janelas conservadoras de 24 horas/31 dias e não conhecem consumo externo da conta. Retry-After fica em memória; reinícios durante bloqueios exigem manter a coleta desabilitada até o prazo expirar. Decisões de revisão dependem da preservação de Logs, pois o esquema não tem FK/status de revisão de evidências. Não há recoleta de empresas já enriquecidas, atualização automática nem novas fontes nesta etapa. Essas limitações estão detalhadas no guia operacional.
