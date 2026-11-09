# Implantação no Render

O serviço Docker compila o `dockerfile`. Ele não executa `compose.yaml`: não cria o SQL Server, não traduz as variáveis do Compose e não compartilha a rede Docker local. O arquivo `.env` é excluído da imagem; importe suas variáveis em **Environment > Add from .env**, depois salve e implante.

## Banco

Configure no serviço web:

```env
ConnectionStrings__DefaultConnection=Server=tcp:HOST_REAL_DO_SQL_SERVER,1433;Database=LeadScopeB2B;User Id=USUARIO;Password=SENHA;Encrypt=True;TrustServerCertificate=False
```

Também é aceito `SQLSERVER_CONNECTION_STRING`. O nome nativo acima tem prioridade. Use credenciais reais somente no painel privado. `MSSQL_SA_PASSWORD` configura o container SQL do Compose, mas não cria banco nem configura a conexão no Render.

`sqlserver`, `localhost` e `host.docker.internal` não apontam para o banco da sua máquina/VPS a partir do Render. Use o hostname acessível do SQL Server remoto e libere a porta no firewall para as origens do serviço. A aplicação usa Microsoft SQL Server; uma URL de Render Postgres não é compatível. Inicialize o schema conforme o README caso o banco seja novo.

O erro `11001 / Name or service not known` significa que o hostname não foi resolvido. Corrija DNS/hostname antes de investigar senha, tabelas ou permissões. O startup agora rejeita configurações ausentes, malformadas e nomes locais conhecidos no Render, sem imprimir credenciais. Outros erros de DNS/rede ainda dependem da infraestrutura.

## Recuperação por Resend

Estas variáveis agora são lidas diretamente, inclusive fora do Compose:

```env
RECUPERACAO_HABILITADO=true
RECUPERACAO_PROVEDOR=Resend
RESEND_API_KEY=CHAVE_PRIVADA
RESEND_REMETENTE=conta@seu-dominio.com
```

Use remetente autorizado no Resend. O sistema usa `RENDER_EXTERNAL_URL` quando nenhuma URL pública foi configurada. Para domínio personalizado, configure `RECUPERACAO_URL_PUBLICA=https://seu-dominio.com`. Template opcional: `RESEND_TEMPLATE_ID` e `RESEND_VARIAVEL_LINK` (padrão `RESET_URL`). O template deve estar publicado e aceitar `username` e a variável do link.

Os nomes nativos `RecuperacaoAcesso__Habilitado`, `RecuperacaoAcesso__Provedor`, `RecuperacaoAcesso__UrlPublica` e `EmailResend__*` continuam aceitos e têm prioridade sobre os aliases, inclusive valores vazios ou `false`. Remova configurações antigas conflitantes. A recuperação depende também do acesso ao banco para localizar a conta e salvar a nova senha. Nenhuma credencial é embutida na imagem e o aplicativo não carrega arquivos `.env` automaticamente.

## Chaves de cookies e links

Para manter cookies e links após reiniciar/reimplantar, monte um disco persistente e configure `DataProtection__DiretorioChaves` para um diretório nesse disco (por exemplo `/var/data/keys` quando o disco está montado em `/var/data`). Um diretório efêmero do container não preserva as chaves.

O erro de desserialização antiforgery é separado do DNS do banco. A perda das chaves pode invalidar cookies e links antigos. Após corrigir, abra uma nova sessão/limpe os cookies antigos. Instâncias simultâneas precisam compartilhar as mesmas chaves.

## Verificação

```sh
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --config
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --email
```

Esses testes não acessam o banco nem enviam e-mail real. Após implantar, teste cadastro/login e solicite a recuperação de uma conta existente. A página de confirmação não prova entrega: confira o worker nos logs e o envio no Resend.

Referências: [variáveis e importação de .env](https://render.com/docs/configure-environment-variables), [Docker no Render](https://render.com/docs/docker), [RENDER_EXTERNAL_URL](https://render.com/docs/environment-variables).
