# LeadScopeB2B — versão acadêmica

Aplicação ASP.NET Core MVC (.NET 10), com SQL Server e ADO.NET puro. Organiza empresas, pessoas e vínculos Pessoa–Empresa, com pesquisa, revisão humana, importação e exportação CSV, autenticação e auditoria.

Para hospedar no Render, consulte [configuração do banco, recuperação de senha e chaves persistentes](Docs/Render.md).

## Execução

1. Instale o SDK .NET 10 e disponibilize SQL Server 2022.
2. Crie uma base vazia chamada `LeadScopeB2B` e, conectado a ela, execute `Data/LeadScopeB2B.sql`. O script não cria a base e não deve ser executado em uma base com dados. Já inclui os perfis necessários, sem criar usuários. As definições das 14 tabelas correspondem a `Docs/Database/LeadScopeB2B.sql`.
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

Para a implantação privada no Coolify com login por e-mail no Cloudflare Access, consulte [Deploy no Coolify](Docs/DeployCoolify.md). A composição de produção é independente da composição local abaixo.

### Primeira execução em outra máquina

Instale Docker com Docker Compose v2 em uma máquina compatível com a imagem SQL Server 2022 (x86-64), com memória disponível para o banco e a aplicação. Não precisa instalar .NET ou SQL Server diretamente no host.

Crie um `.env` privado na raiz do projeto. Exemplo com recuperação por Resend:

```env
MSSQL_SA_PASSWORD=SubstituaPorUmaSenhaForte_123!
RECUPERACAO_HABILITADO=true
RECUPERACAO_PROVEDOR=Resend
RECUPERACAO_URL_PUBLICA=http://localhost:18080
RESEND_API_KEY=re_substitua_pela_chave_real
RESEND_REMETENTE=conta@seu-dominio.com
# Opcionais, para template publicado:
# RESEND_TEMPLATE_ID=seu-template
# RESEND_VARIAVEL_LINK=RESET_URL
```

Use uma senha que atenda à política de complexidade do SQL Server e um remetente autorizado no Resend. Sem recuperação, basta configurar `MSSQL_SA_PASSWORD`. O Compose monta a conexão automaticamente usando essa senha; não precisa editar código ou `appsettings.json`. Nunca versione o `.env`.

Inicie o banco e aguarde o healthcheck indicar `healthy`:

```sh
docker compose up -d sqlserver
docker compose ps
```

Somente na primeira instalação, com volume novo, execute na raiz do projeto os comandos abaixo. A senha é obtida do ambiente do container:

```sh
docker compose exec -T sqlserver sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "CREATE DATABASE [LeadScopeB2B]"'
docker compose exec -T sqlserver sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d LeadScopeB2B' < Data/LeadScopeB2B.sql
docker compose build leadscope
```

Crie o primeiro administrador antes de disponibilizar a aplicação. O Compose atual não repassa `LEADSCOPE_ADMIN_*` do `.env`; nesta etapa única, informe os valores pelo terminal Bash e repasse ao container temporário:

```bash
read -r -p 'Nome do administrador: ' LEADSCOPE_ADMIN_NOME
read -r -p 'E-mail do administrador: ' LEADSCOPE_ADMIN_EMAIL
read -r -s -p 'Senha do administrador: ' LEADSCOPE_ADMIN_SENHA
echo
export LEADSCOPE_ADMIN_NOME LEADSCOPE_ADMIN_EMAIL LEADSCOPE_ADMIN_SENHA
docker compose run --rm -e LEADSCOPE_ADMIN_NOME -e LEADSCOPE_ADMIN_EMAIL -e LEADSCOPE_ADMIN_SENHA leadscope --criar-admin
unset LEADSCOPE_ADMIN_NOME LEADSCOPE_ADMIN_EMAIL LEADSCOPE_ADMIN_SENHA
docker compose up -d leadscope
```

Acesse `http://localhost:18080`. SQL para ferramentas no host: `localhost,14331`. O Compose não cria automaticamente a base, as tabelas ou o primeiro administrador.

### Atualizações, dados e acesso externo

Após alterar código ou `.env`, execute `docker compose up -d --build leadscope`. Reiniciar não atualiza as variáveis do container. Alterar `MSSQL_SA_PASSWORD` no `.env` não troca a senha de um SQL Server já inicializado; altere-a no banco e mantenha os valores sincronizados.

Os volumes `sqlserver_data` e `dataprotection_keys` preservam banco e chaves de cookies/recuperação na mesma máquina. Não execute `docker compose down -v` para preservar esses dados. Copiar o projeto para outra máquina não copia os volumes: transfira um backup do banco e, para manter sessões e links existentes, as chaves. Em uma base restaurada, não repita os comandos de criação e schema acima. O volume histórico `integracoes_data` permanece declarado, sem montagem ou uso pela aplicação.

O `compose.yaml` é de desenvolvimento: usa `Development` e publica as portas somente em `127.0.0.1`. Outra máquina não acessará a aplicação diretamente. Publicação na internet exige configuração de produção, domínio, HTTPS e proxy confiável. Alterar `RECUPERACAO_URL_PUBLICA` só muda os links; não abre portas nem configura HTTPS. Para produção, siga [Deploy no Coolify](Docs/DeployCoolify.md). No [Render](Docs/Render.md), publicar somente a imagem da aplicação exige SQL Server separado.

A recuperação usa SMTP ou Resend. Os nomes `RECUPERACAO_*`, `RESEND_*` e `SMTP_*` são suportados; os nomes nativos do .NET, como `RecuperacaoAcesso__Habilitado`, têm prioridade. A aplicação não carrega `.env` automaticamente: o Compose lê o arquivo e repassa as variáveis declaradas em `environment`. Fora do Compose, configure as variáveis no ambiente do processo ou da hospedagem. Em produção a URL pública precisa usar HTTPS. Consulte [Contas](Docs/Contas.md) para SMTP e templates Resend.

## Validação

```sh
dotnet restore
dotnet build -c Release
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --contrato
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --config
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --email
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj
```

O último comando exige `LEADSCOPE_TEST_CONNECTION` ou conexão de desenvolvimento com permissão para criar banco temporário. Testa SQL Server, HTTP MVC, papéis, CSV, CNAE, exclusão lógica e recuperação SMTP local. Cria e remove apenas sua base temporária. Não use `dotnet test` como evidência de execução: o projeto de testes é um executável.

## Escopo e histórico

Não há API JSON, recebimento externo, conectores REST, enriquecimento, OAuth de provedores, contratos OpenAPI, cotas ou coleta automática nesta entrega. `FonteDados` e `DadosColetados` permanecem no esquema de 14 tabelas, sem configurar ou executar conectores. Integrações externas são possibilidade futura.

O motor anterior foi preservado na branch `archive/integracoes-antes-simplificacao`, commit `999a224`, publicada no GitHub. As correções da versão acadêmica ficam em `branch-kevin`.

## Interface visual

A interface reproduz os tokens e componentes do ZIP de referência em Razor, Tailwind CSS v4, SVG Lucide e JavaScript nativo. CSS, fontes e ícones são locais e pré-compilados; Node.js não é necessário no servidor .NET ou no container publicado.

Para regenerar os recursos após editar a interface: `npm ci --ignore-scripts && npm run build`. Para validar no Chrome com banco temporário: `dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --visual`. Consulte [Interface visual e produção](Docs/InterfaceVisual.md) para arquitetura, dependências, diferenças em relação ao protótipo e detalhes de reprodução.
