# LeadScopeB2B

Aplicação ASP.NET Core MVC (.NET 10), SQL Server e ADO.NET com `Microsoft.Data.SqlClient`. Fluxo Controller → Service → Repository → banco, com SQL parametrizado e operações assíncronas.

## Banco de dados

O contrato da aplicação é [Docs/Database/LeadScopeB2B.sql](Docs/Database/LeadScopeB2B.sql), com 14 tabelas: `ConjuntoPermissoes`, `FonteDados`, `PerfisAcesso`, `Usuarios`, `Logs`, `Cnaes`, `Pessoas`, `Empresas`, `DadosColetados`, `CnaesEmpresas`, `PessoasEmpresas`, `Revisoes`, `Exportacoes` e `ExportacoesPessoasEmpresas`.

A aplicação usa diretamente os nomes e campos desse banco. Não executa DDL, não renomeia estruturas e não modifica o esquema na inicialização. Os dados de referência de permissões estão em [InsertCPPA.sql](Docs/Database/InsertCPPA.sql). O banco existente deve estar provisionado pelo operador.

## Executar

Configure a conexão em User Secrets ou em `ConnectionStrings__DefaultConnection` no ambiente:

```bash
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=localhost,14331;Database=LeadScopeB2B;User Id=<usuario>;Password=<senha>;TrustServerCertificate=True'
dotnet restore
dotnet build
dotnet run
```

A porta deve corresponder ao SQL Server utilizado. O Compose do projeto expõe a porta local 14331. Em produção, use HTTPS e certificado válido no SQL Server.

Para criar o primeiro administrador com o perfil associado ao conjunto `Admin`:

```bash
export LEADSCOPE_ADMIN_NOME='Seu nome'
export LEADSCOPE_ADMIN_EMAIL='voce@empresa.com'
read -rsp 'Senha inicial: ' LEADSCOPE_ADMIN_SENHA
export LEADSCOPE_ADMIN_SENHA
dotnet run -- --criar-admin
unset LEADSCOPE_ADMIN_SENHA
```

Não há senha padrão. O cadastro público em `/Conta/Cadastro` concede somente Leitura.

## Funcionalidades

- Contas: cadastro, login/logout, edição do próprio nome/e-mail, troca de senha e recuperação por SMTP configurável. Administradores gerenciam o perfil de acesso. `Usuarios` contém somente `Id`, `Nome`, `Email`, `SenhaHash` e `PerfilAcessoId`; não há bloqueio ou desativação de contas. [Contas](Docs/Contas.md).
- Empresas: cadastro, edição, consulta, filtros e remoção pelo campo `Status`. CNPJ normalizado e validado. A restrição UNIQUE original admite somente um registro com CNPJ nulo.
- Pessoas: cadastro, edição, consulta, bloqueio por `DataBloqueio` e remoção por `DataExclusao`. Duplicidade verificada por e-mail ou `UrlLinkedIn`.
- Leads: registros de `PessoasEmpresas`, com revisão humana e histórico em `Revisoes`. Edição reabre a análise; remoção usa `StatusLead` e `StatusVinculo`.
- CNAEs: consulta, cadastro e associação pela chave `CnaeNumero + EmpresaId`, sem distinção de principal.
- Fontes: cadastro e disponibilidade determinada por `DataHoraExclusao`, atualizando `DataHoraModificacao`.
- Dados coletados: armazenamento e consulta de `Campo`, `Valor`, `NivelConfianca`, `FonteDadosId` e exatamente um destino. O recebimento não sobrescreve cadastros; os cinco campos empresariais importados permitem atualização somente por aprovação humana na interface, auditada em Logs. O esquema não possui estado de aprovação, URL de evidência, valor normalizado separado ou data de coleta nessa tabela. [Integrações](Docs/Integracoes.md).
- Exportação CSV: até 200 leads qualificados com vínculo ativo, pessoa validada/desbloqueada e empresa validada. Registra `QtdItens`, o usuário e os pares exportados na mesma transação, com proteção contra fórmulas.
- Auditoria: eventos em `Logs`, com `TipoEvento`, `DescricaoEvento`, `DataHora`, `DadosAlterados` e `UsuarioId`. Não registra credenciais.

| ConjuntoPermissoes.Nome | Papel | Acesso |
| --- | --- | --- |
| Admin | ADMIN | Administração, cadastros, revisão de leads, exportação e auditoria |
| Gestor | OPERACAO | Consultas, cadastros comerciais, revisão de leads e exportação |
| Leitura | LEITURA | Consulta de empresas, pessoas, leads e CNAEs |

A interface usa cookie HttpOnly, antifalsificação nos POSTs e sessões de oito horas. A sessão é conferida com os dados atuais da conta. A API usa exclusivamente `X-Api-Key`, exige HTTPS fora de Development e limita chamadas por IP a 120/minuto. Não há CORS aberto.

## Testes

```bash
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --contrato
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj
```

A opção `--contrato` confere nomes, tipos, nulabilidade e tamanhos dos models sem acessar o banco. A suíte completa cria um banco temporário `LeadScopeB2B_Tests_<id>`, usando as definições das 14 tabelas do esquema definitivo e os dados de referência. Exercitam SQL Server, services, HTTP, Razor, autenticação, API e SMTP local. Ao final, excluem apenas o próprio banco temporário. Configure `LEADSCOPE_TEST_CONNECTION` para apontar ao servidor autorizado a criar bancos de teste; as tabelas da base de origem não são utilizadas.

O motor genérico de integrações está implementado. Conectores REST/JSON são configurados por contratos e pela interface; veja o guia abaixo.

## Integrações e APIs

Motor REST/JSON por catálogo, OpenAPI 3.0/3.1 (JSON/YAML), assistente manual ou contrato privado LeadScopeB2B 1.0. Administradores configuram autenticação, mapeamento, custos, limites e rotinas na interface. A CNPJAPI é um contrato do catálogo; não há executor exclusivo de fornecedor.

[Conexão simplificada e descoberta de limites](Docs/SimplificacaoIntegracoes.md) · [Guia de configuração e operação](Docs/MotorIntegracoes.md) · [Contrato privado e exemplos](Docs/ContratoIntegracaoLeadScopeB2B.md) · [Entrega e validação](Docs/EntregaMotorIntegracoes.md).

O banco de 14 tabelas permanece intacto; configurações e segredos cifrados ficam em armazenamento auxiliar persistente. Automação e escrita externa permanecem desabilitadas por padrão.
