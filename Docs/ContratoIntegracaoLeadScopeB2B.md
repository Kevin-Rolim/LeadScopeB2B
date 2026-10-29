# Contrato de Integração LeadScopeB2B 1.0

Uma empresa não precisa fornecer biblioteca C# para uma API REST/JSON compatível. Pode disponibilizar OpenAPI 3.0/3.1 e orientar o administrador no mapeamento, ou fornecer o manifesto declarativo LeadScopeB2B com mapeamento e condições conhecidos.

A versão inicial é a string **"1.0"** em `versaoContrato`. Versões desconhecidas e propriedades privadas não reconhecidas são recusadas. Evoluções incompatíveis exigirão nova versão e suporte explícito do importador; não mudarão silenciosamente contratos antigos. A configuração mantém revisão otimista e últimas 20 versões editadas, e pode exportar o contrato sem credenciais.

## Estrutura do manifesto

O [exemplo completo](Exemplos/contrato-privado-1.0.json) pode ser importado diretamente, após a autorização de rede do operador.

| Campo | Finalidade |
| --- | --- |
| versaoContrato | Versão do formato, atualmente "1.0" |
| provedor / categoria / documentacao | Identificação e documentação do serviço |
| urlBase / perfilRede | Base absoluta e perfil previamente permitido no servidor |
| autenticacoes | Métodos/IDs, nome do header/query, endpoints OAuth, escopos e envio do cliente; **sem valores de segredos** |
| plano | Condições, moeda, mensalidade, limites do administrador/sistema e orçamento; autorização começa false |
| erros | Mapa opcional de status 4xx/5xx para códigos declarativos; 401/403/429 mantêm classificação central de segurança |
| operacoes | Lista de operações REST/JSON |

Cada operação contém `id`, `nome`, `metodo`, `caminho`, `autenticacaoId`, `parametros`, `corpo` fixo opcional, `schemaResposta` simples, `permissoes`, `caminhoItens`, `mapeamentos`, `correspondencia`, `paginacao`, `custo`, `rotina` e limites de timeout/retries/itens. Ausências usam defaults conservadores. Operação, rotina e integração nunca são habilitadas apenas por importação.

Parâmetros declaram nome, local (`path/query/header/body`), tipo, formato, obrigatoriedade, tamanho, faixa numérica, enumeração e origem opcional em um cadastro existente. Corpo de negócio convencional é um objeto JSON montado por propriedades. Credenciais nunca devem ser colocadas em defaults, headers comuns, corpo fixo ou parâmetros da rotina.

Mapeamentos declaram caminho seguro no JSON, tipo externo, destino lógico e transformação da lista. Por exemplo, `$.organization.legalName` → `Empresa.RazaoSocial`. Não usar código, filtros JSONPath ou funções arbitrárias. O [guia operacional](MotorIntegracoes.md) descreve exatamente o subconjunto suportado e os destinos persistíveis.

## Como implementar a API da empresa

1. Forneça HTTPS com certificado confiável. Redes internas precisam ser autorizadas por origem exata e CIDR no servidor LeadScopeB2B.
2. Implemente um dos métodos de autenticação suportados. Exponha a chave/token somente por canal seguro ao administrador responsável; não o inclua no manifesto.
3. Aceite os parâmetros declarados e devolva `application/json` com tipos estáveis. Retorne um identificador consultado para permitir verificação de correspondência.
4. Exponha somente informações necessárias ao propósito da integração. Dados de sócios/terceiros não devem ser acrescentados desnecessariamente ao contrato.
5. Use HTTP 400 para parâmetro inválido, 401 para credencial inválida/expirada, 403 para autorização insuficiente, 404 para registro ausente, 429 para limite e 503 para indisponibilidade. Inclua `Retry-After` quando houver prazo. Corpos de erro podem explicar falhas ao seu cliente, mas o LeadScope não os armazena ou exibe integralmente.
6. Documente cotas, unidade de consumo, custo por requisição/item/crédito e permissões/escopos quando conhecidos. Preços desconhecidos devem permanecer nulos; plano e mensalidade não são obrigatórios. Não afirme gratuidade ou ilimitado sem base contratual.
7. Se houver paginação, declare parâmetro de página/offset/cursor e caminho do próximo cursor. Não dependa de URLs arbitrárias de próxima página.
8. Se disponibilizar consulta de cota, declare uma operação com `consultaCota.caminhoRpm` e/ou `caminhoRestante`, e o significado de nulo se aplicável. Não é obrigatório ter endpoint de cota.
9. Publique OpenAPI ou manifesto versionado. Valide-o na interface antes de habilitar e teste uma operação com credencial de menor privilégio compatível.

Autenticação proprietária, HMAC, mutual TLS específico por cliente, composição complexa de schemas, protocolos especiais ou serialização não suportada exigem adaptador revisado no projeto. Não existe promessa de executar automaticamente toda API que tenha documentação OpenAPI.

## Executar a demonstração local

A [API Python de demonstração](Exemplos/api_privada.py) usa somente a biblioteca padrão e devolve dados fictícios. É um processo de teste independente, não um componente executado pelo motor e não um novo container.

```bash
# No host, em ambiente de desenvolvimento. Gere e guarde uma chave própria.
export DEMO_API_KEY='<chave de demonstração escolhida por você>'
python3 Docs/Exemplos/api_privada.py
```

Ela escuta apenas `127.0.0.1:8015`. Para testar com o MVC via `dotnet run` no mesmo host, o operador autoriza:

```bash
export MotorIntegracoes__RedesPrivadasJson='[{"id":"demo-local","origens":["http://127.0.0.1:8015"],"redes":["127.0.0.1/32"],"permitirHttp":true}]'
```

Na interface:

1. Importe `contrato-privado-1.0.json` e selecione `demo-local`.
2. Confira URL base, operação e mapeamento; informe uma fonte existente se for persistir evidências.
3. Cadastre em `erp` a mesma chave do ambiente de demonstração; não a cole no contrato.
4. Autorize as condições e habilite integração/operação revisadas.
5. No teste, informe `registration=00776574000156`. O retorno é fictício e não constitui consulta real dessa empresa. Para evidências, selecione uma empresa de demonstração já existente com esse CNPJ; não use a resposta fictícia como dado factual do banco acadêmico.
6. Consulte resultado/histórico. A automação permanece desabilitada, salvo autorização específica.

Também pode importar `openapi-privada-3.1.yaml`, configurar a chave e mapear os campos pela interface. OpenAPI convencional não define automaticamente o significado de negócio do mapeamento ou os preços; o administrador precisa completá-los.

No Compose, localhost aponta ao container, não ao host: para APIs privadas corporativas use o DNS/IP real autorizado. O perfil de loopback acima destina-se exclusivamente à demonstração local. O runtime de produção não executa o script Python.

Os próprios arquivos de exemplo são importados e executados contra fixtures nos testes automatizados, incluindo autenticação e mapeamento. Não é necessário modificar Controllers, Services ou Repositories para conectá-los.

## Descoberta e conexão simplificada

Declare `capacidades.operacaoCota` com o ID da operação GET de consumo. `consultaCota` pode incluir `caminhoCompetencia`, `caminhoUsado`, `caminhoLimite`, `caminhoRestante`, `caminhoRpm`, `caminhoRenovacao`, `formatoRenovacao` (data com fuso explícito, segundos ou unix) e `atualizarMinutos`. Declare `restanteNuloSemLimite`/`limiteZeroSemLimite` somente se essa for a semântica do fornecedor. `naoConsomeCota=true` separa consultas administrativas do contador empresarial local apenas quando a operação é GET com custo conhecido zero. Sem essa informação, a proteção conservadora continua aplicável. Veja o [guia de descoberta automática](SimplificacaoIntegracoes.md).
