# LeadScopeB2B — versão acadêmica

Aplicação ASP.NET Core MVC (.NET 10), com SQL Server e ADO.NET puro. Organiza empresas, pessoas e vínculos Pessoa–Empresa, com pesquisa, revisão humana, importação e exportação CSV, autenticação e auditoria.

## Execução

1. Instale o SDK .NET 10 e disponibilize SQL Server 2022.
2. Em uma base nova, execute `Data/LeadScopeB2B.sql`. As definições das 14 tabelas correspondem a `Docs/Database/LeadScopeB2B.sql`. `Docs/Database/InsertCPPA.sql` contém apenas referências de perfis.
3. Em uma base anterior, execute **somente** `Data/AdequarPerfis.sql` antes de usar esta versão. O script renomeia os conjuntos Admin → Administrador, Gestor → Revisor e Leitura → Usuário, preservando IDs e relacionamentos. Revise a atribuição dos usuários: o antigo Gestor passa a revisar e o antigo Leitura passa a cadastrar.
4. Configure `ConnectionStrings:DefaultConnection` por user secrets ou `ConnectionStrings__DefaultConnection` no ambiente. Não versione credenciais.
5. Crie o primeiro administrador com `dotnet run -- --criar-admin`, fornecendo `LEADSCOPE_ADMIN_NOME`, `LEADSCOPE_ADMIN_EMAIL` e `LEADSCOPE_ADMIN_SENHA` no ambiente. Não existe senha padrão; o comando recusa uma segunda inicialização.
6. Execute `dotnet restore`, `dotnet build -c Release` e `dotnet run`.

## Atores

| Ator | Acesso |
|---|---|
| Usuário | Consulta, pesquisa, cadastros, manutenção de vínculos, importação e exportação CSV |
| Revisor | Consulta, pesquisa e decisão de revisão dos vínculos |
| Administrador | Todos os recursos, administração de usuários e atribuição dos perfis, auditoria |

O cadastro público concede Usuário. Senhas usam PasswordHasher; cookies são revalidados a cada requisição. Alterações do vínculo invalidam sua revisão atual, mantendo o histórico. Somente Revisor e Administrador podem qualificar ou rejeitar vínculos. Exclusões de cadastros são lógicas.

## CNAE e CSV

CNAE é característica da empresa: pesquisa combinada com nome/CNPJ e status, consulta nos detalhes e associação/desassociação na edição. Pesquise o catálogo por código ou descrição; vínculos repetidos são ignorados. Cadastre a empresa antes de associar CNAEs. O catálogo SQL existente é preservado; não há módulo independente de CNAEs.

Use **Importar CSV**, também acessível nas listas de empresas e pessoas. Arquivos UTF-8 separados por `;`, com aspas para campos que contenham separadores ou quebras de linha. Limites: 1 MiB e 1000 registros por arquivo. Cada linha válida é salva independentemente; erros e duplicidades são apresentados por registro. Reenvios não atualizam registros existentes.

- Empresas: `RazaoSocial;Cnpj;NomeFantasia;Email;Telefone;Cidade;Estado`. RazaoSocial e Cnpj obrigatórios; demais colunas opcionais.
- Pessoas: `Nome;Email;Telefone;UrlLinkedIn`. Nome e Email ou UrlLinkedIn obrigatórios; demais colunas opcionais.

Duplicidades usam CNPJ para empresas e e-mail/LinkedIn para pessoas, inclusive cadastros removidos. Pessoas importadas registram `Origem=Importação CSV`; não é necessário cadastrar fontes. A exportação está na lista de leads, para até 200 selecionados qualificados, com vínculo ativo, pessoa validada e desbloqueada e empresa validada, com registro de auditoria e proteção de células contra fórmulas.

## Docker e recuperação de acesso

Defina `MSSQL_SA_PASSWORD` no `.env` privado e execute `docker compose up -d --build`. Inicialize o banco conforme acima. Aplicação: `http://localhost:18080`; SQL: `localhost,14331`. Volumes de banco e chaves são mantidos. O volume histórico `integracoes_data` permanece declarado, sem montagem ou uso pela aplicação, para preservar instalações anteriores. Não execute `down -v` se quiser preservar dados.

A recuperação de acesso é opcional e usa SMTP, independente das integrações de dados removidas. Configure `RecuperacaoAcesso:Habilitado`, `RecuperacaoAcesso:UrlPublica` e `EmailSmtp` no ambiente. Sem SMTP, login, cadastro e administração continuam disponíveis. Consulte [Contas](Docs/Contas.md).

## Validação

```sh
dotnet restore
dotnet build -c Release
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --contrato
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj
```

O último comando exige `LEADSCOPE_TEST_CONNECTION` ou conexão de desenvolvimento com permissão para criar banco temporário. Testa SQL Server, HTTP MVC, papéis, CSV, CNAE, exclusão lógica e recuperação SMTP local. Cria e remove apenas sua base temporária. Não use `dotnet test` como evidência de execução: o projeto de testes é um executável.

## Escopo e histórico

Não há API JSON, recebimento externo, conectores REST, enriquecimento, OAuth de provedores, contratos OpenAPI, cotas ou coleta automática nesta entrega. `FonteDados` e `DadosColetados` permanecem no esquema de 14 tabelas, sem configurar ou executar conectores. Integrações externas são possibilidade futura.

O motor anterior foi preservado na branch `archive/integracoes-antes-simplificacao`, commit `999a224`, publicada no GitHub. As correções da versão acadêmica ficam em `branch-kevin`.

## Interface visual

A interface reproduz os tokens e componentes do ZIP de referência em Razor, Tailwind CSS v4, SVG Lucide e JavaScript nativo. CSS, fontes e ícones são locais e pré-compilados; Node.js não é necessário no servidor .NET ou no container publicado.

Para regenerar os recursos após editar a interface: `npm ci --ignore-scripts && npm run build`. Para validar no Chrome com banco temporário: `dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --visual`. Consulte [Interface visual e produção](Docs/InterfaceVisual.md) para arquitetura, dependências, diferenças em relação ao protótipo e detalhes de reprodução.
