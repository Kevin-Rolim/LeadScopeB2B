# LeadScope no Coolify com Cloudflare Access

Endereço: **https://leadscopeb2b.trinyx.com.br**. O domínio alternativo foi usado porque a conexão Cloudflare disponível permite administrar o DNS de `trinyx.com.br`.

O Cloudflare Access solicita um código por e-mail e permite somente `lucaskevin287@gmail.com`, com sessão de oito horas. Depois dessa etapa, continua valendo o login próprio do LeadScope e suas permissões. As contas e senhas existentes foram preservadas.

## Caminho das requisições

Navegador → Cloudflare Access → túnel `trinyx-gateway` → `127.0.0.1:18080` → LeadScope → SQL Server privado.

A regra do túnel para esse hostname exige a validação do JWT do Access, com o audience exclusivo do LeadScope. Os demais hostnames do túnel mantêm sua configuração. O proxy público do Coolify não recebe uma rota para esta aplicação.

## Configuração implantada

| Recurso | Configuração |
|---|---|
| Coolify | Projeto `Aula`, ambiente `production`, serviço `LeadScopeB2B` |
| UUID do serviço | `cjyozelfbzi7og3g3vncwoen` |
| Compose | `compose.coolify.yaml`, usado sozinho |
| Imagem da aplicação | Compilada no servidor com `dockerfile.coolify`; tag registrada no Coolify em `LEADSCOPE_IMAGE` |
| Execução | Usuário `app`, raiz somente leitura, sem capabilities, `no-new-privileges` |
| Entrada da aplicação | Somente `127.0.0.1:18080`; porta interna 8080 |
| SQL Server | Sem portas publicadas; imagem existente fixada por digest em `LEADSCOPE_SQL_IMAGE` |
| Banco persistente | Volume externo `leadscopeb2b_sqlserver_data` |
| Chaves persistentes | Volume externo `leadscopeb2b_dataprotection_keys`, proprietário UID/GID 1654 |
| Protocolo encaminhado | Somente proxy confiável `172.30.71.1`, gateway da rede `edge` |
| Segredos | Senha existente armazenada como variável protegida no Coolify |

O Coolify acrescenta sua rede própria do serviço. As duas definições têm `traefik.enable=false` e nenhum domínio configurado no proxy. A rede `database` é interna. A subnet `172.30.71.0/29` foi conferida contra as redes existentes antes da implantação.

## Atualizações

1. Compile uma imagem com tag nova: `docker build -f dockerfile.coolify -t leadscopeb2b:<tag> .`.
2. Atualize `LEADSCOPE_IMAGE` no serviço `LeadScopeB2B` do Coolify e implante esse serviço.
3. Confirme SQL saudável, aplicação respondendo pelo túnel e redirecionamento de visitantes anônimos para o Access.

A imagem fica no Docker deste servidor; não há publicação em registry nem atualização automática pelo GitHub. Ao mover de servidor, será necessário recompilar ou transferir essa imagem e os volumes.

Não atribua domínio direto à aplicação no proxy do Coolify: a entrada configurada é pelo túnel. Se alterar a rede `edge`, ajuste também o endereço confiável do proxy.

## Preservação e retorno

Antes da implantação foram feitas cópias offline dos dois volumes, com SHA-256, em `artifacts/coolify-20261103/`. Essa pasta é privada e ignorada pelo Git e pelo contexto Docker. `deployment.json` registra as imagens, e `smoke-tests.txt` registra a validação. Esses arquivos constituem a cópia feita nesta implantação; não configuram uma rotina de backup.

Os containers anteriores `leadscopeb2b-leadscope-1` e `leadscopeb2b-sqlserver-1` continuam parados. Eles compartilham os mesmos volumes: mantenha-os parados enquanto o serviço do Coolify estiver ativo. O `compose.yaml` original foi preservado para execução local.

Para retornar à instalação anterior, pare primeiro apenas o serviço `LeadScopeB2B` no Coolify. Só depois inicie a composição original. Não remova os volumes. Uma reversão que retire a proteção deve remover antes a entrada pública deste hostname; não substitua a regra protegida do túnel por uma entrada sem Access.

Nenhum script de recriação de esquema, alteração de senha ou migração dos dados principais foi executado. Os testes SQL/HTTP usam uma base temporária independente.

## Validação em 03/11/2026

Compilação Release sem avisos ou erros; contrato das 14 tabelas e 103 verificações SQL/HTTP aprovados. A base principal manteve suas contagens após os testes. O login de produção respondeu com cookie antiforgery Secure e rejeitou hostname inválido.

Pela internet, `/`, `/Conta/Login` e `/css/site.css` redirecionaram para o Access. Cabeçalhos de identidade falsos também redirecionaram para o Access. A página de e-mail carregou corretamente; nenhum código foi solicitado durante a validação. O acesso pelo proxy direto retornou 404. O conector confirmou o recebimento da configuração com validação JWT. As 18 regras anteriores do túnel foram preservadas, e os containers já ativos mantiveram seus horários de inicialização e portas.
