# LeadScopeB2B — versão acadêmica

Aplicação ASP.NET Core MVC (.NET 10), com SQL Server e ADO.NET puro. Organiza empresas, pessoas e vínculos Pessoa–Empresa, com pesquisa, revisão humana, importação e exportação CSV, autenticação e auditoria.

## Execução

1. Para executar **sem Docker**, instale o SDK .NET 10 e disponibilize SQL Server 2022. Para executar **com Docker**, basta Docker Engine/Desktop com Compose v2; siga a seção abaixo.
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

## Docker — instalação nova em Windows ou Linux

Requisitos: Docker Engine/Desktop com **Compose v2**, CPU `x86_64/amd64`, memória suficiente (o SQL Server precisa de ao menos **2 GB apenas para ele**, além da aplicação) e espaço em disco. Não é necessário instalar SQL Server ou .NET no computador anfitrião. O SQL Server é a edição Developer, destinada a desenvolvimento/testes.

1. Copie `.env.example` para `.env` (`cp .env.example .env` no Linux/macOS ou `Copy-Item .env.example .env` no PowerShell).
2. Edite **as duas senhas**, o e-mail e o nome do administrador. Use senhas fortes, diferentes e privadas. `.env` está no `.gitignore`. Não use ponto e vírgula nem aspas na senha do SQL, pois ela é incorporada à connection string ADO.NET. Se o SQL Server já existir em volume, mantenha a senha `sa` correspondente.
3. Execute `docker compose up -d --build`.
4. Acesse **http://localhost:18080**. SQL Server (opcional para ferramentas externas): **localhost,14331**. As portas estão ligadas a `127.0.0.1`; personalize `LEADSCOPE_HTTP_PORT` e `LEADSCOPE_SQL_PORT` no `.env` se estiverem ocupadas.

O Compose aguarda o SQL Server ficar saudável, executa `sql-init` (cria o banco e as 14 tabelas apenas se ainda não existirem), valida os tipos/colunas, as restrições e os perfis, e então executa `admin-init`. **A primeira conta administrativa só é criada se `Usuarios` estiver vazia**; bases que já têm usuários não recebem senhas ou contas novas. Por fim a aplicação inicia. Se houver incompatibilidade de banco, o processo falha com mensagem de erro **sem apagar dados**. Instalações legadas com perfis antigos podem exigir aplicar manualmente `Data/AdequarPerfis.sql` **após backup e revisão**.

Para conferir os serviços e logs:

```sh
docker compose ps -a
docker compose logs --tail=150 sqlserver sql-init admin-init leadscope
```

O serviço `sql-init` e o `admin-init` aparecerem como `Exited (0)` depois do sucesso é **normal**: são tarefas de inicialização, não servidores permanentes. Não execute `docker compose down -v` caso deseje preservar o banco, os usuários e as chaves de sessão. `docker compose down` sem `-v` preserva volumes. O volume histórico `integracoes_data` continua declarado sem montagem para não afetar instalações anteriores.

Em banco antigo **com usuários**, se não houver administrador, o inicializador **não promove ninguém automaticamente**; use um procedimento administrativo autorizado. Se os containers falharem, confira memória disponível, portas locais, senha do volume e os logs do serviço específico. O healthcheck verifica conectividade, e a validação do esquema não substitui testes funcionais.

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
