# Contas

O modelo segue `Usuarios(Id, Nome, Email, SenhaHash, PerfilAcessoId)`, com o perfil em `PerfisAcesso` e o conjunto em `ConjuntoPermissoes`. As permissões reconhecidas são `Administrador`, `Revisor` e `Usuário`. Não existem campos de status, datas ou versão de sessão em `Usuarios`.

O cadastro público concede o perfil associado a `Usuário`, independentemente de campos extras enviados ao formulário. A administração permite criar usuários e editar nome, e-mail, senha e perfil. A aplicação preserva o último administrador ao alterar perfis. Não oferece bloqueio, desativação ou exclusão de usuários.

Senhas são armazenadas em `SenhaHash` com `PasswordHasher`. Cookies duram oito horas, são HttpOnly e são revalidados a cada requisição contra os dados atuais da conta. A assinatura da sessão deriva de `SenhaHash`, `Nome`, `Email` e `PerfilAcessoId`; alterar esses dados invalida cookies anteriores. Não há contador persistente de sessões. Voltar aos mesmos dados pode tornar válida uma assinatura anterior ainda não expirada; trocas de senha geram outro hash.

O titular altera nome/e-mail informando a senha atual. Alterar a senha encerra as sessões anteriores. Links de recuperação são protegidos com Data Protection, expiram em uma hora e são vinculados aos dados atuais da conta. A troca de senha compara o hash anterior no UPDATE, impedindo que dois pedidos consumam o mesmo link.

A recuperação depende de `RecuperacaoAcesso:Habilitado`, `RecuperacaoAcesso:UrlPublica` e `EmailSmtp` (`Host`, `Porta`, `Remetente`, `UsarTls`, `Usuario`, `Senha`). Em produção a origem pública exige HTTPS. Sem configuração válida a tela informa indisponibilidade e não simula envio. Cadastro, login e troca autenticada de senha funcionam sem SMTP.

Os POSTs da interface exigem antifalsificação. Limites por minuto/IP: login 10, cadastro 5, alterações de conta 10 e redefinição 10. Solicitação de recuperação: 5 por 15 minutos/IP. Eventos são gravados em `Logs`, sem senhas, hashes ou tokens.

O primeiro administrador é criado exclusivamente pelo comando `dotnet run -- --criar-admin`, com `LEADSCOPE_ADMIN_NOME`, `LEADSCOPE_ADMIN_EMAIL` e `LEADSCOPE_ADMIN_SENHA`. O perfil associado a `Administrador` deve existir no banco; não há senha padrão.
