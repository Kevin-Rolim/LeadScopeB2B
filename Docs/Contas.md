# Contas

O modelo segue `Usuarios(Id, Nome, Email, SenhaHash, PerfilAcessoId)`, com o perfil em `PerfisAcesso` e o conjunto em `ConjuntoPermissoes`. As permissões reconhecidas são `Administrador`, `Revisor` e `Usuário`. Não existem campos de status, datas ou versão de sessão em `Usuarios`.

O cadastro público concede o perfil associado a `Usuário`, independentemente de campos extras enviados ao formulário. A administração permite criar usuários e editar nome, e-mail, senha e perfil. A aplicação preserva o último administrador ao alterar perfis. Não oferece bloqueio, desativação ou exclusão de usuários.

Senhas são armazenadas em `SenhaHash` com `PasswordHasher`. Cookies duram oito horas, são HttpOnly e são revalidados a cada requisição contra os dados atuais da conta. A assinatura da sessão deriva de `SenhaHash`, `Nome`, `Email` e `PerfilAcessoId`; alterar esses dados invalida cookies anteriores. Não há contador persistente de sessões. Voltar aos mesmos dados pode tornar válida uma assinatura anterior ainda não expirada; trocas de senha geram outro hash.

O titular altera nome/e-mail informando a senha atual. Alterar a senha encerra as sessões anteriores. Links de recuperação são protegidos com Data Protection, expiram em uma hora e são vinculados aos dados atuais da conta. A troca de senha compara o hash anterior no UPDATE, impedindo que dois pedidos consumam o mesmo link.

A recuperação depende de `RecuperacaoAcesso:Habilitado`, `RecuperacaoAcesso:UrlPublica` e `EmailSmtp` (`Host`, `Porta`, `Remetente`, `UsarTls`, `Usuario`, `Senha`). Em produção a origem pública exige HTTPS. Sem configuração válida a tela informa indisponibilidade e não simula envio. Cadastro, login e troca autenticada de senha funcionam sem SMTP.

Os POSTs da interface exigem antifalsificação. Limites por minuto/IP: login 10, cadastro 5, alterações de conta 10 e redefinição 10. Solicitação de recuperação: 5 por 15 minutos/IP. Eventos são gravados em `Logs`, sem senhas, hashes ou tokens.

O primeiro administrador é criado exclusivamente pelo comando `dotnet run -- --criar-admin`, com `LEADSCOPE_ADMIN_NOME`, `LEADSCOPE_ADMIN_EMAIL` e `LEADSCOPE_ADMIN_SENHA`. O perfil associado a `Administrador` deve existir no banco; não há senha padrão.

## Escolher SMTP ou Resend

`RecuperacaoAcesso:Provedor` aceita `Smtp` (padrão) ou `Resend`. O envio usa somente o provedor selecionado, sem fallback automático. Ambos exigem recuperação habilitada e URL pública HTTPS. Para Resend, configure `EmailResend:ApiKey` e `EmailResend:Remetente`, usando remetente autorizado no provedor. Não versione a chave.

No Coolify, o Compose repassa estas variáveis:

```env
RECUPERACAO_HABILITADO=true
RECUPERACAO_PROVEDOR=Resend
RESEND_API_KEY=re_sua_chave
RESEND_REMETENTE=conta@seu-dominio.com
```

Para SMTP, use `RECUPERACAO_PROVEDOR=Smtp` e configure `SMTP_HOST`, `SMTP_PORTA` (587 por padrão), `SMTP_REMETENTE`, `SMTP_USUARIO` e `SMTP_SENHA`. TLS é obrigatório em produção. Guarde chaves e senhas como segredos no Coolify e reimplante após alterar configurações.

Referência: https://resend.com/docs/api-reference/emails/send-email. Teste local sem envio real: `dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --email`.

### Template de recuperação no Resend

Publique o template no Resend e configure seu assunto lá. No botão de redefinição, use uma variável de link chamada `RESET_URL` (ou configure o nome existente). O sistema preenche essa variável com o link individual e temporário de recuperação.

```env
RESEND_TEMPLATE_ID=id-ou-alias-do-template-publicado
RESEND_VARIAVEL_LINK=RESET_URL
```

Essas variáveis complementam a configuração Resend acima. Fora do Compose, use `EmailResend__TemplateId` e `EmailResend__VariavelLink`. Com template configurado, o sistema envia `template.id` e `template.variables`, sem corpo ou assunto próprios; o assunto vem do template. Sem TemplateId, continua enviando o texto simples anterior. O sistema também envia a variável `username` (exatamente em minúsculas), preenchida com o nome cadastrado da pessoa. Outras variáveis devem ter valores padrão no Resend. Templates são usados apenas pela API Resend, não pelo SMTP.

Este fluxo redefine a senha de uma conta existente. Não implementa confirmação de e-mail no cadastro.

### Container local

O `compose.yaml` também repassa as variáveis `RECUPERACAO_*`, `RESEND_*` e `SMTP_*` do `.env`. A URL local padrão é `http://localhost:18080`; para acessar de outro dispositivo configure `RECUPERACAO_URL_PUBLICA` com o endereço acessível pelo navegador. Após alterar o `.env` ou o código, execute `docker compose up -d --build leadscope` para recriar a aplicação. Apenas reiniciar não atualiza as variáveis do container.
