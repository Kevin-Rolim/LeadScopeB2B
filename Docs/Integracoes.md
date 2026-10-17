# API de integração

A API é parte da mesma aplicação MVC. Não usa microserviços, EF ou provedores fictícios. Todas as chamadas exigem `X-Api-Key`. Em produção, use HTTPS.

## Credenciais

1. Crie um usuário dedicado em **Usuários**, com perfil de leitura ou operação conforme a necessidade. A conta deve estar `ATIVO`.
2. Cadastre a fonte real em **Fontes** e anote seu ID. Envio exige fonte ativa.
3. Gere uma chave aleatória de pelo menos 32 bytes e calcule SHA-256 sobre o texto exato da chave, sem quebra de linha. Guarde a chave na aplicação cliente e apenas o hash no servidor. Por exemplo, execute localmente:

   ```python
   import hashlib, secrets
   chave = secrets.token_urlsafe(32)
   print('Chave para o cliente:', chave)
   print('SHA256 para o servidor:', hashlib.sha256(chave.encode()).hexdigest())
   ```

4. Configure User Secrets (desenvolvimento) ou as variáveis de ambiente equivalentes (`Integracoes__Chaves__0__Sha256` etc.):

   ```bash
   dotnet user-secrets set 'Integracoes:Chaves:0:Sha256' '<sha256 hexadecimal de 64 caracteres>'
   dotnet user-secrets set 'Integracoes:Chaves:0:UsuarioId' '<id do usuário>'
   dotnet user-secrets set 'Integracoes:Chaves:0:FonteDadosId' '<id da fonte>'
   dotnet user-secrets set 'Integracoes:Chaves:0:PermiteEnvio' 'true'
   ```

   Para somente leitura, use `PermiteEnvio=false`. Envio também exige conjunto `Administracao` ou `Operacao Comercial`. Não existe chave habilitada por padrão. Índices 1, 2 etc. permitem outros clientes. Remover/trocar o hash revoga a chave; bloquear ou desativar o usuário também a revoga. Mudanças em variáveis de ambiente exigem reinício do processo.

A comparação usa tempo constante. Cookie de login não autentica a API e chave de API não concede acesso aos formulários. O usuário de integração não pode escolher a identidade do autor ou a fonte no payload.

## Endpoints

| Método | Caminho | Resultado |
| --- | --- | --- |
| GET | `/api/empresas?busca=&status=&cnae=` | Empresas ativas nos filtros; status `INATIVA` ainda é consultável, `REMOVIDA` é omitida |
| GET | `/api/empresas/{id}` | Empresa e CNAEs associados |
| GET | `/api/pessoas?busca=&status=` | Pessoas não removidas |
| GET | `/api/pessoas/{id}` | Pessoa não removida |
| GET | `/api/leads?busca=&status=&pessoaId=&empresaId=&cnae=` | Vínculos não removidos com pessoa/empresa não removida |
| GET | `/api/leads/{pessoaId}/{empresaId}` | Detalhes do vínculo |
| GET | `/api/cnaes?busca=` | Até 200 códigos/descrições |
| POST | `/api/integracoes/dados` | Evidência pendente: `202 Accepted` e `Location` |
| GET | `/api/integracoes/dados/{id}` | Estado da evidência; somente a mesma fonte pode consultar |
| POST | `/api/integracoes/leads` | Novo vínculo incerto: `201 Created` e `Location` |

Os filtros de CNAE aceitam sete dígitos ou formatação usual. JSON usa camelCase. Retornos não contêm senhas/hashes de senha. As consultas refletem o catálogo autorizado, inclusive estados ainda pendentes; o consumidor deve observar os estados retornados.

A recepção atual trabalha com pessoas e empresas já cadastradas: a restrição existente em `Dados_coletados` exige exatamente um destino. Cadastro/edição de pessoas e empresas é feito na interface por usuários autorizados. Não há endpoint externo que aprove evidências, qualifique leads, troque permissões ou apague cadastros.

### Enviar evidência

```http
POST /api/integracoes/dados
X-Api-Key: <chave>
Content-Type: application/json

{
  "pessoaId": 42,
  "campo": "email",
  "valor": "contato@empresa.com",
  "nivelConfianca": 92.5,
  "evidenciaUrl": "https://empresa.com/equipe"
}
```

Ou use `empresaId`, sem `pessoaId`. Identificadores precisam existir, a pessoa não pode estar bloqueada/removida e a empresa não pode estar inativa/removida. `nivelConfianca` é opcional e vai de 0 a 100. URL de evidência é opcional e deve usar HTTP(S). O servidor não acessa a URL recebida.

Campos permitidos:

| Destino | Campos |
| --- | --- |
| Pessoa | `nome`, `email`, `telefone`, `linkedin`, `origem` |
| Empresa | `razao_social`, `nome_fantasia`, `cnpj`, `site`, `segmento`, `porte`, `cidade`, `estado`, `email`, `telefone` |

Valores são validados por campo (tamanho, e-mail, HTTP(S), CNPJ com DV, UF) e normalizados. Campo vazio/desconhecido é recusado. Estados e permissões não são campos de enriquecimento.

Um reenvio com mesma fonte/destino/campo/valor normalizado reutiliza o ID enquanto a evidência estiver pendente. Depois de revisada, um novo envio constitui nova evidência e exige nova decisão. A resposta `202` indica armazenamento para revisão, não confirmação do valor no cadastro.

Em **Revisar dados**, um operador aplica ou rejeita a evidência. Aplicação e mudança de estado ficam na mesma transação com o log. Duplicidade de pessoa/CNPJ e disponibilidade do destino são verificadas novamente; falha reverte toda a operação. `VALIDADO` ou `REJEITADO` são os estados finais da evidência. A aprovação de um campo não valida automaticamente o cadastro completo.

`Revisoes` continua reservado ao vínculo pessoa–empresa, conforme a FK existente. As decisões sobre evidências são registradas em `Logs`, sem alterar relacionamentos.

### Enviar vínculo

```http
POST /api/integracoes/leads
X-Api-Key: <chave>
Content-Type: application/json

{
  "pessoaId": 42,
  "empresaId": 7,
  "cargo": "Gerente comercial",
  "departamento": "Comercial",
  "senioridade": "Sênior",
  "nivelConfianca": 85
}
```

O servidor atribui `statusLead=NOVO`, `statusVinculo=INCERTO` e a fonte da credencial. Pessoa e empresa precisam estar disponíveis. Um par já cadastrado resulta em `409`; a chave composta também impede corrida entre chamadas. Revisão e confirmação do vínculo ocorrem na interface antes de sua exportação.

## Respostas e limites

- `200`: consulta concluída.
- `201` / `202`: vínculo criado / evidência aguardando revisão.
- `400`: JSON/dados inválidos; erros de validação incluem `errors`.
- `401` / `403`: credencial ausente/inválida ou operação sem permissão.
- `404`: registro ausente, removido ou evidência de outra fonte.
- `409`: duplicidade, fonte inativa, destino indisponível ou conflito de revisão.
- `415`: conteúdo enviado sem `application/json`.
- `429`: limite de 120 requisições por minuto/IP excedido.
- `503`: falha de acesso ao banco; detalhes ficam no log interno.

Os erros usam JSON Problem Details, sem SQL, stack trace ou dados de autenticação. Envios têm limite de 32 KiB. Não há CORS liberado para qualquer origem. O catálogo usa filtros simples; não há coleta agendada ou chamadas a terceiros nesta versão.
