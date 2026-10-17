# Validação — 17/10/2026

- `dotnet build` e `dotnet build -c Release --no-restore`: sucesso, zero erros e zero avisos.
- `dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj`: **159 verificações aprovadas**, com SQL Server e servidores HTTP/SMTP de testes reais.
- `dotnet list package --include-transitive --vulnerable`: nenhum pacote vulnerável apontado pelas fontes NuGet consultadas.
- `git diff --check`: sem erros de whitespace.
- Inspeção visual de cadastro, login e recuperação no Google Chrome headless, em desktop (1440×1000) e celular (390×844/1000): formulários, disponibilidade do SMTP e navegação responsiva renderizados corretamente. O servidor usado nesta inspeção foi encerrado ao final.
- Não há referências a EF/DbContext nem métodos de rastreamento/persistência do EF no código executável. Anotações de esquema usadas apenas pelo EF também foram removidas dos models; validações permanecem.

A ampliação de contas adicionou:

- Migração incremental reexecutável sobre usuário já cadastrado: hash, perfil, status e dados preservados.
- Cadastro público HTTP → login com a senha cadastrada, confirmação de senha, validação, duplicidade, hash no banco e recusa de campos forjados de administrador/status/ID.
- Perfil próprio com senha atual, prevenção de e-mail duplicado, recusa de alteração de outra conta/permissões e revogação das outras sessões.
- Troca de senha com senha atual, invalidação de todas as sessões, login com nova credencial, logout e CSRF.
- Bloqueio/desbloqueio pela administração, impedimento de POST administrativo por usuário comum e revogação de cookies mesmo sem acesso durante o período bloqueado.
- Limites de cadastro, login e recuperação, erro genérico de login e indisponibilidade real sem SMTP configurado.
- SMTP TCP local exclusivo dos testes: solicitação anônima uniforme, entrega de link à conta ativa, nenhum envio a conta ausente/bloqueada, origem configurada e formulário sem cache/referrer.
- Redefinição por link válido, token inválido/expirado/de outro propósito, uso único, dois POSTs concorrentes, impedimento para contas bloqueadas/desativadas e invalidação de links após desbloqueio.

Cobertura prévia preservada:

- Comando local de criação do primeiro administrador; proteção contra repetição e contra remoção do último administrador.
- Hash de senha, atualização sem trocar senha, troca efetiva, perfil e desativação.
- Cadastros e atualizações de empresas/pessoas, CNPJ numérico e alfanumérico com DV, CNPJs nulos, duplicidade com formatações diferentes e pessoa duplicada em cadastros concorrentes.
- CNAEs, normalização, pesquisa, associação, principal único e rollback em falha de relacionamento.
- Leads com chave composta, impedimento de duplicidade, revisão/histórico, reabertura de análise na edição, bloqueio e remoções lógicas.
- Exportação com critérios de validação, conteúdo CSV e proteção contra fórmulas.
- Recebimento de evidências, reenvio idempotente enquanto pendente, aplicação humana, rejeição, revisão repetida e rollback de aprovação que causaria duplicidade.
- Busca SQL parametrizada e tentativa de injeção.
- HTTP: login com cookie e antifalsificação; páginas Index/Create/Edit/Details; POSTs de cadastro/edição, erros no formulário e revisão; recursos CSS/JS reais.
- Separação entre cookie e chave da API, perfis de leitura/operação/administração, revogação em sessões existentes após mudança de perfil/senha/estado.
- API: JSON, chave inválida, ausência de credencial, envio sem permissão, exigência de Content-Type, validação de destino/campo, recepção 202/Location, consulta, criação de vínculo 201 e conflito 409.
- Isolamento de evidências por fonte e bloqueio de envio por fonte desativada.
- Auditoria de login, recebimento/revisão e exportação.

Os testes usam um banco temporário exclusivo, aplicando o esquema atual sem tocar nas tabelas da conexão de origem. Após cada execução o banco temporário é excluído. A conferência final da base original manteve os mesmos registros iniciais: três conjuntos de permissões e três perfis; todas as demais tabelas vazias. Nesta ampliação foi adicionada somente a coluna versao_sessao na tabela Usuarios da base de desenvolvimento, por script transacional e repetível. Uma comparação dos campos anteriores de usuários e das contagens de todas as tabelas confirmou a preservação dos dados. Não foram alterados índices ou relacionamentos. Não restaram bancos de teste.

Os testes HTTP exercitam Razor e o carregamento de recursos da interface. A validação automatizada não representa homologação por usuários de negócio nem integração com serviços externos ainda não especificados. A base original permite cadastro comum pela interface e continua aguardando a criação do administrador com uma senha escolhida pelo operador, fontes reais e chaves das aplicações autorizadas. SMTP/origem pública não estão configurados nessa base: a recuperação informa indisponibilidade; os demais fluxos de conta estão operacionais. O SMTP usado na suíte captura mensagens apenas localmente, sem entregar e-mails a destinatários externos.
