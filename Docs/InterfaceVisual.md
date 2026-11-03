# Interface visual e produção

A referência primária é `Design de referencia/pixel-perfect-replica-main.zip`. O arquivo contém um projeto React 19/TanStack com Tailwind CSS 4.2.1, componentes Radix/shadcn e Lucide. Foram examinados `styles.css`, `app-shell.tsx`, `ls.tsx` e as rotas de login, início, empresas e leads. Nenhum script do ZIP foi executado no projeto MVC.

## Adaptação

A implementação mantém Razor e os fluxos HTTP MVC. Cores OKLCH, fontes IBM Plex Sans/Mono, sidebar de 240 px, marca, espaçamentos, bordas de 2–4 px, botões de 36/28 px, tabelas compactas, badges, barras de confiança, painel lateral e diálogo de confirmação seguem os componentes do ZIP. Bootstrap e jQuery foram removidos por completo.

Componentes compartilhados em `Views/Shared`: logo, ícones, sidebar, cabeçalho de página, filtros, indicadores de status/confiança, estado vazio, notificações, confirmações e painel. `Frontend/site.css` centraliza os tokens e componentes; `wwwroot/css/site.css` é o resultado compilado e minificado. Ícones são um sprite SVG local de Lucide; fontes WOFF2 e licenças estão em `wwwroot/fonts`.

O layout autenticado é responsivo, com menu lateral móvel, foco por teclado e retorno de foco ao fechar. Confirmações e painéis usam `<dialog>` com foco modal nativo. Escape, confirmação de descarte, validação de campos, visibilidade de senha e estados de envio são implementados em JavaScript nativo. As mensagens de servidor continuam sendo exibidas e os POSTs mantêm seus tokens CSRF.

Links de cadastro, edição e detalhes abrem painéis por melhoria progressiva: o JavaScript carrega o HTML da rota MVC existente, extrai o conteúdo e mantém formulários e ações reais. Ao salvar, o formulário faz um POST MVC normal. As mesmas URLs funcionam como páginas completas sem JavaScript. Não há SPA, autenticação no cliente ou servidor frontend em produção.

Todas as telas existentes foram adaptadas: login/cadastro/recuperação/redefinição/perfil/senha; dashboard; listas, formulários, detalhes e remoções de empresas/pessoas/leads; bloqueio de pessoa; CNAEs na empresa; revisão e histórico; CSV; usuários e auditoria. Páginas de erro e acesso negado seguem o mesmo sistema visual. O dashboard usa os Services existentes para consultar indicadores reais, sem novas tabelas ou alterações de regra de negócio.

## Compilação reproduzível

Node.js é ferramenta de desenvolvimento, sem uso no servidor publicado:

```sh
npm ci --ignore-scripts
npm run build
dotnet restore
dotnet build -c Release
dotnet publish -c Release
```

`npm run assets` copia somente ícones/fontes usados e respectivas licenças. `npm run build:css` usa o Tailwind CLI v4.2.1 e detecta classes nos arquivos Razor e JS locais. As versões estão fixadas no `package-lock.json`. Os arquivos gerados de `wwwroot` devem ser versionados junto das fontes. O build .NET verifica os arquivos visuais essenciais e inclui os estáticos na publicação; não chama Node. O Docker utiliza esses estáticos prontos. Não há Play CDN, recursos de fonte remotos nem JavaScript de React no produto publicado.

## Diferenças necessárias em relação ao protótipo

- Não há contas de demonstração, dados persistentes fictícios ou resultados simulados.
- Pessoas, usuários, CSV e auditoria ganham telas com o mesmo design system, pois não possuem rota equivalente no ZIP.
- Os campos e estados são os reais do backend. Empresa pode ter vários CNAEs; a associação continua na edição após o cadastro. A lista mantém Segmento, pois o ViewModel atual não expõe CNAEs por linha. O catálogo de seleção é o banco existente, sem lista fixa do protótipo.
- O backend não expõe data de entrada na lista de leads: o dashboard informa ordenação por pessoa, sem simular recentes. Contagem de pessoas usa os registros reais de Pessoas.
- As abas de leads filtram no servidor, preservando os demais filtros; não exibem contagens globais fictícias de uma lista já filtrada.
- Os painéis retornam ao fluxo MVC após salvar. Não há atrasos artificiais nem atualização que altere somente o estado visual.

## Testes de navegador

```sh
# Exige SQL Server e conexão com permissão para banco temporário:
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --visual
```

O runner usa uma base temporária, cria fixtures somente nela, inicia a aplicação MVC e chama `scripts/test-ui.mjs`. O Playwright usa Chrome em `/usr/bin/google-chrome`, ou `CHROME_PATH`. Ele valida desktop 1440×1000 e celular 390×844; navegação, painéis, descarte, formulários, filtros, bloqueio, revisão com confirmação, download/upload CSV, remoção lógica e perfis. Capturas e resultados ficam em `artifacts/ui`, ignorados no Git. As contas e registros das capturas são fixtures do banco temporário, removidas ao terminar.

A referência visual foi também renderizada separadamente em `/tmp` para comparação de login, empresas e início, usando seus componentes originais. Esse renderizador não faz parte do repositório nem da aplicação publicada. Não há alegação de igualdade pixel a pixel entre páginas com dados e funcionalidades diferentes.

## Resultado executado nesta revisão

- `npm ci --ignore-scripts` e `npm run build`: aprovados. A comparação SHA-256 confirmou CSS e sprite SVG idênticos após uma instalação limpa e recompilação.
- `dotnet restore` e `dotnet build -c Release`: aprovados, sem avisos ou erros.
- Runner completo com `--visual`: aprovado, com 104 verificações reportadas, incluindo a execução do navegador; contratos das 14 tabelas preservados. O banco temporário foi removido pelo runner.
- Chrome: 67 verificações e 38 capturas em desktop/celular; nenhum erro JavaScript e nenhuma falha de CSS/JS/fontes/ícones. Foram incluídos acessos diretos a detalhes/edição, além dos fluxos de painel e ações reais.
- Inspeção visual: capturas da implementação comparadas com renderizações isoladas dos componentes originais de login, empresas e início. Os ajustes de rodapé dos painéis e seções do formulário foram incorporados antes da rodada final.
- `docker compose config --quiet` e `docker compose build leadscope`: aprovados.
- Execução com `docker compose run` em container temporário: login, CSS, JavaScript, fonte e sprite SVG responderam HTTP 200. Confirmada imagem de execução sem Node.js, sem Bootstrap e sem scripts de teste publicados. O container temporário foi removido.
- `node --check` nos scripts e `git diff --check`: aprovados.

Não foram alterados o esquema, os dados persistentes, Services ou Repositories nesta refatoração visual. As alterações da simplificação anterior foram mantidas no working tree. Não houve publicação ou implantação permanente desta revisão.
