# Conexão simplificada de integrações

## Uso

Em **Integrações e APIs → Catálogo**, adicione uma API, informe sua credencial, selecione as operações e conecte. O catálogo fornece endereço, autenticação e operações. Campos vazios de credencial preservam o segredo existente; o frontend nunca recebe o valor armazenado. Ative ou desative a integração e a automação na mesma tela.

Não é necessário informar plano, mensalidade, moeda, orçamento ou limites contratados. Informações técnicas, mapeamento, limites manuais e teto financeiro permanecem em **Opções avançadas**, sem perder as funcionalidades anteriores. Operações de escrita continuam exigindo revisão e autorizações específicas.

Para uma API externa, importe OpenAPI 3.0/3.1 ou um contrato privado, ou informe URL, método/endpoint e autenticação no cadastro REST. Nome e configurações adicionais são opcionais. Parâmetros indispensáveis, operações e mapeamento devem ser revisados quando não forem conhecidos pelo contrato. O [guia completo](MotorIntegracoes.md) descreve autenticação, importação, mapeamento, backup e limites de compatibilidade.

## Descoberta automática

O mecanismo é compartilhado. `ContratoApi.Capacidades` pode declarar `operacaoCota`, `limitesDeclarados`, origem (`contrato`/`catalogo`), documentação, data de verificação e gratuidade documentada. A operação referenciada declara `consultaCota`, com caminhos JSON seguros para RPM, saldo, uso, limite, competência e renovação, quando disponíveis. Contratos privados podem usar os mesmos campos; não precisam de biblioteca C#.

A prioridade é endpoint oficial declarado, headers reconhecidos, metadados do contrato/catálogo e proteção local. Não são procurados endpoints por adivinhação e OpenAPI comum não implica descoberta de consumo. Apenas consultas de cota GET, habilitadas e revisadas, com custo explicitamente zero são executadas automaticamente.

A tela distingue:

- **Confirmado:** observado em endpoint ou header, com data da consulta.
- **Documentado:** declarado no contrato/catálogo, com sua origem.
- **Local:** proteção do LeadScopeB2B, nunca apresentada como plano contratado.
- **Desconhecido:** informação que o provedor não disponibilizou.

Sem limites informados, aplica-se proteção local de 6 chamadas/minuto, 20 consultas/dia e 200/mês. Limites locais menores ou conhecidos são respeitados. Os contadores locais usam UTC; isso não define a janela de cobrança do fornecedor. Limites documentados diários/mensais sem janela são avaliados conservadoramente com buckets cobrindo pelo menos 24 horas/um mês.

Headers suportados incluem os nomes explicitamente configurados e `X-RateLimit-Limit-Minute`. A combinação `RateLimit-Policy` ou `RateLimit-Limit` com janela explícita (`quantidade;w=segundos`), `RateLimit-Remaining` e `RateLimit-Reset` em segundos permite controlar a janela. Um header genérico sem unidade/janela não é interpretado como RPM ou cota mensal. Outros formatos exigem declaração ou adaptação.

O estado confirmado permanece separado da projeção local de saldo após novas chamadas. Limites e cooldown são compartilhados entre consultas e automação e persistidos em arquivo. `429` significa limitação, sem presumir esgotamento mensal; `Retry-After` determina a espera. Sem ele, há espera local conservadora. Não há repetição indiscriminada de consultas cobradas.

## CNPJAPI

Segundo a [documentação oficial de cota](https://cnpjapi.com.br/docs/cota), `GET https://api.cnpjapi.com.br/cota` usa Bearer e não debita consultas empresariais. O contrato do catálogo interpreta `competencia`, `usado`, `limite`, `restante` e `rpm`. `limite=0` e `restante=null` indicam cota ilimitada conforme a semântica documentada; isso não elimina RPM nem proteções locais.

A conexão consulta esse endpoint sem realizar enriquecimento empresarial. O resultado fica em cache por 30 minutos; tentativas com falha aguardam pelo menos 5 minutos e respeitam cooldown. A tela, a execução de operações e o worker de integrações com automação ativa atualizam dados vencidos. A atualização manual também respeita o cache.

Consultas administrativas declaradas explicitamente como não consumidoras de cota não gastam o limite local empresarial, mas respeitam espaçamento/cooldown e proteção própria de 100/dia e 2.000/mês. Esse tratamento é declarativo e compartilhado, sem executor exclusivo da CNPJAPI.

O endpoint não informa nome do plano, mensalidade nem instante exato de renovação. O sistema não infere esses dados a partir da competência ou da cota. Dados de consumo podem ficar defasados se a mesma chave for utilizada fora do LeadScopeB2B.

## Custos e automação

Preço ausente permanece **desconhecido**, nunca gratuito por inferência. Uma consulta manual exige confirmação de possível cobrança; a automação exige autorização própria. Orçamento é opcional. Se o administrador definir um teto com preço desconhecido, deverá configurar uma reserva conservadora para permitir chamadas; a reserva não garante o valor real cobrado.

A ativação da automação na interface registra autorização explícita por integração e utiliza o BackgroundService existente. Parâmetros vindos de empresas/pessoas e fonte de evidências compatível são preparados conforme o contrato. Parâmetros fixos indispensáveis são solicitados. Não se criam cadastros nem se aprovam evidências automaticamente.

O padrão é desabilitado. `COLETA_HABILITADA` continua controlando rotinas antigas configuradas somente no editor; não revoga uma autorização explícita por integração na interface. Para impedir uma integração autorizada, desative sua automação ou a própria integração. A escrita automática continua dependendo também da autorização do servidor.

## Persistência e validação

Contratos, capacidades, estado da conexão, snapshots de consumo e autorizações ficam no armazenamento auxiliar existente, com gravação atômica e controle de concorrência. Credenciais permanecem cifradas separadamente. O volume de integrações e as chaves Data Protection precisam ser preservados no backup/restauração. Não houve alteração de tabelas, colunas, índices, relacionamentos ou scripts definitivos.

A suíte usa provedores HTTP/TLS simulados e bancos SQL temporários isolados. Nenhum teste consome a CNPJAPI real. O catálogo descreve integração implementada com validação simulada, sem afirmar certificação do fornecedor.

Arquivos desta simplificação: modelos/validação/catálogo e transporte em `Integration/Universal`; serviços existentes de consumo, executor, gerenciador, automação e worker, com arquivos parciais de descoberta; repository de fontes; controller e view models de integrações; views administrativas e JavaScript condicional; testes universais/MVC e documentação. Docker Compose, containers, volumes e esquema acadêmico foram preservados.
