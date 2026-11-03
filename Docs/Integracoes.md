# API de integração

A API pertence à aplicação MVC e usa diretamente o esquema de [LeadScopeB2B.sql](Database/LeadScopeB2B.sql). Todas as chamadas exigem `X-Api-Key`; em produção exigem HTTPS.

## Credenciais

Cadastre um usuário dedicado e uma fonte em `FonteDados`. Gere uma chave aleatória de pelo menos 32 bytes, guarde-a no cliente e configure somente seu SHA-256 hexadecimal no servidor:

```bash
dotnet user-secrets set 'Integracoes:Chaves:0:Sha256' '<sha256 da chave>'
dotnet user-secrets set 'Integracoes:Chaves:0:UsuarioId' '<id do usuário>'
dotnet user-secrets set 'Integracoes:Chaves:0:FonteDadosId' '<id da fonte>'
dotnet user-secrets set 'Integracoes:Chaves:0:PermiteEnvio' 'true'
```

Variáveis de ambiente equivalentes usam `Integracoes__Chaves__0__Sha256` etc. Índices adicionais configuram outros clientes. Não há chave padrão. Envio exige `PermiteEnvio=true`, perfil `Admin` ou `Gestor` e fonte com `DataHoraExclusao` nula. Leitura aceita também `Leitura`. Remover ou trocar o hash revoga a chave; mudanças nas variáveis exigem reiniciar o processo. A comparação usa tempo constante. Cookie não autentica a API e a chave não autentica formulários.

## Endpoints

| Método | Caminho | Resultado |
| --- | --- | --- |
| GET | `/api/empresas?busca=&status=&cnae=` | Empresas não removidas |
| GET | `/api/empresas/{id}` | Empresa e CNAEs |
| GET | `/api/pessoas?busca=&status=` | Pessoas não removidas |
| GET | `/api/pessoas/{id}` | Pessoa |
| GET | `/api/leads?busca=&status=&pessoaId=&empresaId=&cnae=` | Vínculos não removidos |
| GET | `/api/leads/{pessoaId}/{empresaId}` | Detalhes do vínculo |
| GET | `/api/cnaes?busca=` | Até 200 códigos |
| POST | `/api/integracoes/dados` | Dado coletado: `201 Created` e `Location` |
| GET | `/api/integracoes/dados/{id}` | Dado coletado da mesma fonte |
| POST | `/api/integracoes/leads` | Vínculo: `201 Created` e `Location` |

JSON usa camelCase. As consultas incluem cadastros pendentes; observe os estados retornados.

## Dados coletados

```http
POST /api/integracoes/dados
X-Api-Key: <chave>
Content-Type: application/json

{
  "pessoaId": 42,
  "campo": "Email",
  "valor": "contato@empresa.com",
  "nivelConfianca": 92.5
}
```

Use `pessoaId` ou `empresaId`, nunca ambos. O destino deve existir e estar disponível. O autor e `FonteDadosId` vêm da credencial. O valor é validado e normalizado antes de ser gravado em `Valor`.

`Campo` deve corresponder exatamente ao nome do campo do banco:

| Destino | Campos |
| --- | --- |
| Pessoas | `Nome`, `Email`, `Telefone`, `UrlLinkedIn`, `Origem` |
| Empresas | `RazaoSocial`, `NomeFantasia`, `Cnpj`, `UrlSite`, `Segmento`, `Porte`, `Cidade`, `Estado`, `Email`, `Telefone` |

`DadosColetados` possui somente `Id`, `Campo`, `Valor`, `NivelConfianca`, `FonteDadosId`, `PessoaId` e `EmpresaId`. Envios idênticos da mesma fonte, campo e destino reutilizam o registro existente. Não há colunas de estado de revisão, URL de evidência, data de coleta ou valor normalizado separado nessa tabela. O recebimento não altera o cadastro de destino. Em **Dados coletados**, os campos empresariais RazaoSocial, NomeFantasia, Porte, Cidade e Estado permitem comparação, aprovação ou rejeição humana; a decisão fica em Logs. Os demais campos permanecem disponíveis para consulta e edição pelos formulários próprios.

## Vínculos

```json
{
  "pessoaId": 42,
  "empresaId": 7,
  "cargo": "Gerente comercial",
  "departamento": "Comercial",
  "senioridade": "Sênior",
  "nivelConfianca": 85
}
```

Envie a `/api/integracoes/leads`. O vínculo nasce com `StatusLead=NOVO`, `StatusVinculo=INCERTO` e a fonte da credencial. O par existente retorna `409`. A revisão ocorre na interface e é registrada em `Revisoes`, vinculada a `PessoasEmpresas`.

## Limites e erros

Até 120 chamadas/minuto/IP; envios de até 32 KiB. Respostas: `400` para dados inválidos, `401` para chave ausente/inválida, `403` para falta de permissão, `404` para ausência/remoção ou dado de outra fonte, `409` para duplicidade ou restrição do banco, `415` para conteúdo sem JSON, `429` para limite e `503` para indisponibilidade do banco. Erros usam Problem Details e não expõem SQL nem credenciais.

A API externa não oferece endpoints de aprovação de evidências ou qualificação de leads. A revisão de evidências empresariais ocorre somente na interface MVC autenticada. A consulta agendada à CNPJAPI é um fluxo de saída separado das credenciais X-Api-Key de entrada, dentro dos mesmos services e repositories existentes.

## Motor de saída — Integrações e APIs

A interface administrativa configura APIs REST/JSON por catálogo, OpenAPI 3.0/3.1 JSON/YAML, formulário manual ou contrato privado versionado. Um executor, uma autenticação compartilhada, um controle de consumo e um BackgroundService atendem todos os provedores. A CNPJAPI é somente um contrato do catálogo.

Consulte [configuração, protocolos, limites, custos e automação](MotorIntegracoes.md) e o [contrato para empresas provedoras](ContratoIntegracaoLeadScopeB2B.md). A automação começa desabilitada. A configuração antiga ColetaAutomatica/CNPJAPI precisa ser migrada explicitamente pela interface; não há execução paralela do cliente antigo.
