# Entrega acadêmica final

Base: `branch-kevin`, commit `999a224e78c5e41a8ba70e38c11d6ab549955830`. Antes da primeira alteração foi criada e publicada `archive/integracoes-antes-simplificacao`, apontando para esse commit. Diagramas locais não rastreados, configurações privadas e volumes persistentes foram preservados.

Foram removidos os controllers MVC de integrações/fontes/CNAEs, controller de API JSON, motor universal, autenticação e credenciais de provedores, contratos OpenAPI, catálogo externo, descoberta e controle de cotas, enriquecimento, coleta automática, repositórios e armazenamento auxiliar do motor, componentes visuais e YamlDotNet. SMTP e seu worker de recuperação de acesso permanecem como funcionalidade de conta. Pkcs permanece porque também é dependência do SqlClient.

Usuário mantém cadastros, manutenção, pesquisa, importação e exportação. Revisor consulta e decide vínculos; Administrador possui todos esses recursos, atribui perfis na administração de usuários e consulta auditoria. Cadastro público concede Usuário. Revisão exige política própria e verificação do perfil no repositório dentro da transação. Cadastro não pode aprovar vínculo. Alteração do vínculo reinicia sua situação e preserva histórico.

CNAEs continuam no banco e na pesquisa combinada de empresas. Associação e desassociação ficam na edição da empresa, usando catálogo pesquisável por código ou descrição e proteção contra duplicidade. A página independente foi removida.

A versão anterior não oferecia importação CSV de cadastros. Foi implementada importação básica de empresas e pessoas por Services e Repositories ADO.NET, com validação, identificação de duplicidades e resultado por linha. Há limites de tamanho e quantidade; registros válidos são salvos independentemente. Não exige fonte configurável e não realiza coleta. A exportação mantém suas regras de qualificação e auditoria.

Validação executada em SQL Server 2022 real, com bancos temporários separados da base existente:

- `dotnet restore`: aprovado.
- `dotnet build -c Release`: aprovado, sem avisos ou erros.
- Contrato dos modelos das 14 tabelas: aprovado; definições originais em `Docs/Database/LeadScopeB2B.sql` preservadas.
- Executável de testes: 103 verificações SQL/HTTP aprovadas, além dos contratos das 14 tabelas. Inclui adequação legada idempotente preservando IDs, proteção do último administrador, três perfis, POST de revisão, CSV multipart, duplicidades, filtros CNAE combinados, associação/desassociação, exportação, exclusão lógica, auditoria, login, cadastro, sessões e recuperação SMTP local.
- `docker compose config --quiet` e `docker compose build leadscope`: aprovados.
- Imagem Docker iniciada em container temporário: `/Conta/Login` respondeu HTTP 200; container removido após teste.
- Busca no código ativo: nenhum HttpClient, registro AddHttpClient, ApiController, motor ou worker de coleta. Rotas antigas responderam 404 nos testes autenticados.

Na primeira tentativa de execução completa, a conexão de desenvolvimento não estava acessível. Os testes foram executados novamente usando o SQL Server Docker local. Falhas intermediárias de fixtures e comparação de URL de redirecionamento foram corrigidas; o resultado acima corresponde à execução final completa.

Para atualizar uma instalação existente, execute `Data/AdequarPerfis.sql` antes de iniciar a nova versão e confira as atribuições dos usuários: Gestor passa a Revisor; Leitura passa a Usuário. Esse script foi testado em base temporária e não foi aplicado à base persistente do ambiente. A recuperação de acesso exige SMTP configurado no ambiente de destino. A publicação da versão acadêmica e sua implantação não foram realizadas; a branch histórica foi publicada conforme solicitado.
