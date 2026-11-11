# LeadScopeB2B — versão acadêmica

Aplicação ASP.NET Core MVC (.NET 10), com SQL Server e ADO.NET puro. Organiza empresas, pessoas e vínculos Pessoa–Empresa, com pesquisa, revisão humana, importação e exportação CSV, autenticação e auditoria.

## Execução

### Início rápido com Docker

Use uma máquina x86-64 com Git e Docker Compose atualizado. No Linux, inicie o Docker Engine. No Windows, abra o Docker Desktop em modo **Linux containers** e execute o comando diretamente no PowerShell. [Docker Desktop para Windows](https://docs.docker.com/desktop/setup/install/windows-install/) · [Requisitos da imagem do SQL Server](https://learn.microsoft.com/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver16)

```sh
git clone https://github.com/Kevin-Rolim/LeadScopeB2B.git
cd LeadScopeB2B
```

**Windows (PowerShell)**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\iniciar.ps1
```

**Linux ou WSL (Bash)**

```bash
bash iniciar.sh
```

Os dois scripts geram a senha local do SQL Server no `.env` na primeira execução. O Docker Compose cria a base `LeadScopeB2B`, instala as tabelas, views, procedures, funções, trigger e índices e só então inicia a aplicação. Quando solicitado, informe nome, e-mail e senha do primeiro administrador. Abra **http://localhost:18080** e entre com esses dados.

A primeira execução baixa as imagens e compila o projeto. Para iniciar novamente, repita o comando do seu sistema. O banco e as chaves da aplicação ficam em volumes Docker. Preserve o `.env` junto com os volumes.

Com um `.env` que já define `MSSQL_SA_PASSWORD`, também é possível iniciar diretamente com `docker compose up -d --build --wait`. Para configurar o primeiro administrador nesse fluxo, use `docker compose exec leadscope dotnet LeadScopeB2B.dll --criar-admin`.

- Para parar, use `docker compose stop`.
- Para consultar o banco com uma ferramenta externa, conecte a `localhost,14331`.
- Para diagnosticar a inicialização, use `docker compose ps -a` e `docker compose logs db-init sqlserver`. O serviço `db-init` termina com código 0 quando conclui o trabalho.

### Execução sem Docker

1. Instale o SDK .NET 10 e disponibilize SQL Server 2022.
2. Para uma instalação nova, crie a base com `CREATE DATABASE [LeadScopeB2B];` e execute `Data/LeadScopeB2B.sql` conectado a ela.
3. Execute `Data/ObjetosLeadScopeB2B.sql` para instalar ou atualizar os objetos. Use o SSMS no Windows ou o `sqlcmd` no Windows e no Linux. Para rodar os arquivos com `sqlcmd`, use `-b -f 65001 -d LeadScopeB2B -i CAMINHO_DO_ARQUIVO` junto com as opções de conexão.
4. Configure `ConnectionStrings:DefaultConnection` por user secrets ou `ConnectionStrings__DefaultConnection` no ambiente. Execute `dotnet run -- --criar-admin` para criar o primeiro administrador e `dotnet run` para iniciar a aplicação.

Em uma base legada que ainda usa os conjuntos `Admin`, `Gestor` e `Leitura`, execute `Data/AdequarPerfis.sql` uma vez e revise as atribuições dos usuários. Os nomes passam a ser `Administrador`, `Revisor` e `Usuário`.

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

Para a implantação privada no Coolify com login por e-mail no Cloudflare Access, use a composição de produção descrita em [Deploy no Coolify](Docs/DeployCoolify.md).

A recuperação de acesso é opcional e usa SMTP ou a API do Resend, independente das integrações de dados removidas. Configure `RecuperacaoAcesso:Habilitado`, `RecuperacaoAcesso:UrlPublica` e `EmailSmtp` no ambiente. Sem provedor de e-mail configurado, login, cadastro e administração continuam disponíveis. Consulte [Contas](Docs/Contas.md).

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
