# Contas, cadastro público e recuperação de acesso

Todos os fluxos utilizam `Usuarios`, `Perfis_acesso`, `Conjuntos_permissoes`, `IPasswordHasher<Usuarios>` e o cookie já existente. A persistência continua em ADO.NET; não há outro sistema de autenticação nem Entity Framework.

## Banco e implantação

O esquema conectado foi examinado antes da alteração. `Usuarios` já tinha nome, e-mail único, `senha_hash`, status, perfil e datas. A única coluna nova é `versao_sessao BIGINT NOT NULL DEFAULT(0)`.

Em instalações anteriores, aplique [001_UsuariosVersaoSessao.sql](Database/Migrations/001_UsuariosVersaoSessao.sql) com um cliente SQL no banco correto antes de executar esta versão da aplicação. O script é transacional e repetível; mantém todas as linhas, hashes, índices e relacionamentos. Instalações novas usam [EsquemaAtual.sql](Database/EsquemaAtual.sql), que já contém o campo. A aplicação não altera o esquema automaticamente.

A migração foi aplicada ao banco de desenvolvimento configurado, com comparação dos campos anteriores de usuários e das contagens de todas as tabelas antes/depois. Os dados foram preservados. Nenhuma conta de teste foi criada na base original.

A versão é incrementada em alterações administrativas da conta, perfil pessoal ou senha. O cookie e os links de recuperação usam a versão junto à impressão do hash da senha. Isso invalida sessões e links anteriores mesmo após bloquear e desbloquear uma conta sem que ela tente acessar durante o bloqueio. Cookies da versão anterior do aplicativo precisam de novo login.

## Fluxos operacionais

| Página | Acesso | Comportamento |
| --- | --- | --- |
| `/Conta/Cadastro` | Público | Nome, e-mail, senha e confirmação; cria conta ATIVO no conjunto Leitura |
| `/Conta/Login` | Público | Erro genérico para senha incorreta, conta ausente, bloqueada ou desativada |
| `/Conta/Perfil` | Autenticado | Consulta e edição do próprio nome/e-mail; exige senha atual |
| `/Conta/AlterarSenha` | Autenticado | Exige senha atual e confirmação da nova; encerra todas as sessões |
| `/Conta/Logout` | POST autenticado | Encerra a sessão no navegador; exige CSRF |
| `/Conta/RecuperarAcesso` | Público | Solicita recuperação somente quando e-mail/origem pública estão configurados |
| `/Conta/RedefinirSenha?token=...` | Público com link válido | Nova senha e confirmação, sem alterar permissões ou reativar contas |
| `/Usuarios` | Administrador | Lista, edita perfil/permissões/status, bloqueia, desbloqueia e desativa contas |

O cadastro público não aceita ID, status ou perfil do formulário: o repository busca explicitamente um perfil no conjunto `Leitura`. Se esse perfil não existir, o cadastro fica indisponível; não há fallback para administrador. A primeira conta pública também é de leitura.

Duplicidades consideram e-mail normalizado para minúsculas, inclusive contas bloqueadas/desativadas. O índice único e a transação protegem cadastros concorrentes. Senhas usam o PasswordHasher já adotado, com salt e hashing PBKDF2. A política existente de 6 a 100 caracteres foi mantida; confirmação é validada também no servidor.

Após cadastro, o usuário é direcionado ao login e pode entrar imediatamente. Sem URL local de retorno, administrador segue para Usuários, operação para Leads e leitura para Empresas. URLs externas de retorno não são aceitas. As políticas de autorização continuam verificadas no servidor, inclusive em POSTs forjados.

O perfil usa exclusivamente o ID do cookie e atualiza somente nome/e-mail. Salvar renova o cookie atual e encerra as demais sessões. Alteração ou recuperação de senha encerra todas as sessões e exige novo login. Nome/e-mail/permissões continuam preservados na troca de senha. Todas as contas bloqueadas/desativadas são rejeitadas pelo login, pelo cookie e pela API.

O primeiro administrador continua sendo criado pelo comando local `dotnet run -- --criar-admin`, com as variáveis `LEADSCOPE_ADMIN_NOME`, `LEADSCOPE_ADMIN_EMAIL` e `LEADSCOPE_ADMIN_SENHA`. Ele pode ser criado depois de usuários de leitura, desde que ainda não exista um administrador cadastrado. Uma conta pública jamais recebe esse privilégio. A proteção do último administrador ativo foi mantida.

## Configurar recuperação por e-mail

**Nesta instalação não há SMTP de produção configurado.** A página informa indisponibilidade e não oferece envio; um POST válido retorna 503, sem alegar que alguma mensagem foi enviada. Cadastro, login, perfil, senha autenticada e administração funcionam sem SMTP.

Para habilitar, configure uma origem pública real e um servidor SMTP autorizado. Exemplo de chaves em User Secrets para desenvolvimento (ou variáveis com `__` em produção):

```bash
dotnet user-secrets set 'RecuperacaoAcesso:Habilitado' 'true'
dotnet user-secrets set 'RecuperacaoAcesso:UrlPublica' 'https://leadscope.suaempresa.com'
dotnet user-secrets set 'EmailSmtp:Host' '<servidor SMTP real>'
dotnet user-secrets set 'EmailSmtp:Porta' '587'
dotnet user-secrets set 'EmailSmtp:Remetente' '<email remetente autorizado>'
dotnet user-secrets set 'EmailSmtp:Usuario' '<usuario SMTP>'
dotnet user-secrets set 'EmailSmtp:Senha' '<senha SMTP>'
dotnet user-secrets set 'EmailSmtp:UsarTls' 'true'
```

O adapter usa SMTP/STARTTLS, não TLS implícito da porta 465 nem OAuth SMTP. Um relay autorizado sem autenticação pode omitir usuário e senha juntos. Fora de Development são exigidos STARTTLS e origem HTTPS. Não há remetente ou credencial fictícia habilitada por padrão.

A origem vem de configuração, não do cabeçalho Host recebido. Pode incluir o caminho-base de publicação; não deve conter credenciais, query ou fragmento. Persista as chaves de Data Protection da aplicação em ambiente estável; a opção `DataProtection:DiretorioChaves` permite indicar um diretório privado fora de `wwwroot`. As mesmas chaves e o nome de aplicação `LeadScopeB2B` devem ser mantidos entre reinícios/instâncias para preservar cookies e links válidos.

## Solicitação e redefinição

- A requisição pública valida o e-mail e coloca a solicitação em uma fila local limitada a 100 entradas, sem consultar se a conta existe. A resposta é igual para contas existentes, ausentes, bloqueadas ou desativadas.
- O worker da própria aplicação procura a conta ativa e envia a mensagem pelo SMTP configurado. A confirmação pública significa recebimento da solicitação; não garante entrega do e-mail. O evento `RECUPERACAO_EMAIL` só é gravado após o servidor SMTP aceitar a mensagem. Falhas ficam no log técnico, sem destinatário, senha, token ou link.
- A fila fica em memória e não sobrevive a reinícios. Em caso de perda/falha de envio, é possível solicitar novamente após o limite aplicável; não há serviço externo ou infraestrutura de filas obrigatória.
- O link tem validade de uma hora e usa Data Protection com propósito exclusivo, associado ao usuário, e-mail e versão da conta. O token não é devolvido pela página de solicitação ou pelo log.
- O formulário verifica token/expiração/conta e pede a nova senha duas vezes. O UPDATE compara hash anterior e versão dentro da transação, impedindo reutilização e consumo concorrente do link. Uma recuperação bem-sucedida invalida todos os links anteriores da conta.
- Mudança de perfil/e-mail/senha ou bloqueio/desbloqueio também invalida links anteriores. Recuperação não desbloqueia contas e não troca papéis.
- Páginas de conta não permitem cache e usam `Referrer-Policy: no-referrer`. POSTs têm CSRF e limite de 32 KiB. Login realiza verificação de hash também para contas ausentes/indisponíveis, sem resposta que confirme a existência da conta.

Limites por IP: login 10/minuto, cadastro 5/minuto, alteração de perfil/senha 10/minuto, solicitação de recuperação 5/15 minutos e redefinição 10/minuto. Um bloqueio por excesso retorna 429 com mensagem de espera.

## Verificação

`dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj` executa testes com SQL Server e HTTP reais em banco temporário isolado. A suíte cobre cadastro público → login, perfil, senha, logout, duplicidades, tentativas de elevar permissões, administração, bloqueio/desbloqueio, revogação persistente, ausência de SMTP, envio SMTP local real, links inválidos/expirados/reutilizados e redefinição concorrente. O servidor SMTP de captura existe somente no projeto de testes e não é utilizado pela aplicação normal. Nenhum e-mail é enviado a destinatários externos durante os testes.

Referências: [Data Protection com validade limitada](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/limited-lifetime-payloads), [persistência das chaves](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings), [SendMailAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.sendmailasync?view=net-10.0).
