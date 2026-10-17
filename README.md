# LeadScopeB2B

Aplicação ASP.NET Core MVC (.NET 10), SQL Server e ADO.NET com `Microsoft.Data.SqlClient`. A arquitetura continua Controller → Service → Repository → banco, com injeção de dependências, SQL parametrizado e operações assíncronas. Não há Entity Framework Core ou migrações automáticas.

## Executar

1. Instale o SDK .NET 10 e disponibilize um SQL Server com o esquema atual.
2. Configure a conexão com User Secrets em desenvolvimento ou com `ConnectionStrings__DefaultConnection` no ambiente de execução:

   ```bash
   dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=localhost,14330;Database=LeadScopeB2B;User Id=<usuario>;Password=<senha>;TrustServerCertificate=True'
   dotnet restore
   dotnet build
   ```

   A conexão local existente foi preservada em User Secrets durante esta implementação. `TrustServerCertificate=True` é apenas para o ambiente local. Em produção, configure TLS/certificado válido e HTTPS para a aplicação; as chaves de API exigem HTTPS fora de Development.

3. Em instalações anteriores, aplique [001_UsuariosVersaoSessao.sql](Docs/Database/Migrations/001_UsuariosVersaoSessao.sql) no banco existente. Essa migração adiciona somente a versão de sessão e preserva os dados. No banco local configurado ela já foi aplicada. O snapshot de instalações novas já contém a coluna.

4. Se ainda não houver administrador cadastrado, crie o primeiro pelo comando local:

   ```bash
   export LEADSCOPE_ADMIN_NOME='Seu nome'
   export LEADSCOPE_ADMIN_EMAIL='voce@empresa.com'
   read -rsp 'Senha inicial: ' LEADSCOPE_ADMIN_SENHA
   export LEADSCOPE_ADMIN_SENHA
   dotnet run -- --criar-admin
   unset LEADSCOPE_ADMIN_SENHA
   ```

   A senha deve ter de 6 a 100 caracteres; use uma senha forte. O comando exige um perfil no conjunto `Administracao` (ou o alias histórico `Admin`) e funciona enquanto não houver administrador cadastrado, mesmo que existam usuários comuns. Não há senha padrão nem cadastro público de administradores. Os hashes fictícios do seed antigo não autenticam.

5. Rode `dotnet run`, abra a URL indicada no terminal e use `/Conta/Cadastro` para criar uma conta de leitura ou `/Conta/Login` para entrar.

O banco conectado foi inspecionado e preservado. Para uma **instalação nova em banco vazio**, um operador pode executar [EsquemaAtual.sql](Docs/Database/EsquemaAtual.sql). Esse snapshot contém tabelas, índices, restrições, perfis e conjuntos básicos, sem usuários, credenciais ou dados comerciais. Não o execute na base existente. Os demais scripts SQL e o PDF são históricos e incompatíveis em vários nomes/colunas; veja [o diagnóstico](Docs/Development/EstadoInicial.md).

## Funcionalidades e regras

- Contas: cadastro público com confirmação de senha, login/logout, perfil pessoal e alteração de senha. Cadastro público sempre recebe Leitura; somente administradores mudam permissões e bloqueios. [Fluxos, migração e SMTP](Docs/Contas.md).
- Recuperação: formulário e redefinição por link temporário de uso único, integrados ao SMTP configurável. Sem configuração, a tela informa indisponibilidade e não simula envio. Os demais fluxos de conta funcionam sem serviço de e-mail.
- Empresas: cadastro, consulta, edição, busca por nome/CNPJ/segmento/cidade, filtro por status e CNAE e remoção lógica. CNPJ opcional, normalizado sem pontuação, numérico ou alfanumérico com dígitos verificadores. `cnpj_raiz` acompanha a alteração. O índice filtrado do banco permite vários CNPJs nulos.
- Pessoas: cadastro, consulta, edição, busca, validação, bloqueio/desbloqueio e remoção lógica. E-mail e perfil profissional identificam duplicidade, inclusive em registros removidos. Nome ou telefone isolados não são usados para unir pessoas automaticamente.
- Leads: vínculo `pessoa_id + empresa_id`, com cargo, departamento, senioridade, fonte, confiança e estados independentes de vínculo e lead. Há pesquisa, filtros por pessoa/empresa/CNAE, edição, revisão com histórico e remoção lógica.
- CNAEs: consulta por código/descrição e cadastro pelo administrador; associação/desassociação na empresa. Código normalizado para sete dígitos e no máximo um principal por empresa, preservando o índice existente.
- Fontes: cadastro e ativação/desativação pelo administrador. Cada integração usa uma fonte efetivamente cadastrada; nenhuma integração de terceiros foi simulada.
- Dados externos: evidências pendentes em `Dados_coletados`, normalização, deduplicação, fila de revisão e aplicação/rejeição humana. A evidência permanece armazenada e o cadastro só muda quando aprovada. Revisões concorrentes e aplicação duplicada são impedidas por transação. Veja [a API](Docs/Integracoes.md).
- Exportação: selecione até 200 leads na lista para gerar CSV. Exige lead `QUALIFICADO`, vínculo `ATIVO`, pessoa `VALIDADO` e desbloqueada, e empresa `VALIDADA`. Grava `Exportacoes`, pares exportados e log na mesma transação, e neutraliza células interpretáveis como fórmulas.
- Auditoria: últimos 200 eventos de login, cadastro público, alterações de perfil/senha, aceitação SMTP da recuperação, recebimento/revisão de evidências, revisão de leads e exportações. Não registra senhas, hashes de senha ou chaves.

Estados de pessoas: `PENDENTE`, `VALIDADO`, `BLOQUEADO`, `REMOVIDO`. O bloqueio é controlado por `data_bloqueio`; edição comum não o remove. Desbloquear volta para `PENDENTE`, exigindo validação. Empresas: `PENDENTE`, `VALIDADA`, `INATIVA`, `REMOVIDA`. Os formulários de edição fazem a validação humana dos cadastros.

Vínculos: `INCERTO`, `ATIVO`, `INATIVO`. Leads começam em `NOVO` ou `EM_ANALISE`; `QUALIFICADO`/`REJEITADO` só são atribuídos pela revisão. Editar reabre a análise e limpa a data da revisão atual, preservando o histórico. A remoção usa `REMOVIDO` e inativa o vínculo, pois não existe coluna de exclusão e há histórico referenciando sua chave. Identificadores de registros removidos continuam reservados; não há restauração automática.

Datas são gravadas e exibidas em UTC. Listas de CNAEs, evidências e auditoria mostram até 200 resultados; as demais listas retornam os registros que satisfazem os filtros.

## Acesso

| Conjunto real do banco | Papel | Permissões |
| --- | --- | --- |
| `Administracao` | ADMIN | Cadastros, revisões, exportações, fontes, CNAEs, usuários e auditoria |
| `Operacao Comercial` | OPERACAO | Consultas, cadastros de pessoas/empresas/leads, associação de CNAEs, revisões e exportações |
| `Leitura` | LEITURA | Consultas de empresas, pessoas, leads e CNAEs |

Os aliases históricos `Admin` e `Gestor` são aceitos explicitamente. Conjuntos desconhecidos não concedem acesso. Usuários têm `ATIVO`, `BLOQUEADO` ou `DESATIVADO`; exclusão de usuário desativa a conta e preserva histórico. O último administrador ativo não pode ser desativado ou perder seu perfil.

A interface usa cookie HttpOnly, proteção antifalsificação nos POSTs e sessões de oito horas. Perfil, estado e versão de sessão são relidos a cada requisição; alterações na conta revogam cookies e links antigos, inclusive após bloqueio seguido de desbloqueio. A API aceita somente a chave própria, separada do cookie. Há limites por IP de login, cadastro, perfil e recuperação, descritos em [Contas](Docs/Contas.md), e de 120 chamadas de API/minuto. Não há CORS aberto.

## Testes

```bash
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj
dotnet list package --include-transitive --vulnerable
```

O executável de testes usa SQL Server real, inclui envio por SMTP local de captura e cria um banco de nome aleatório começando por `LeadScopeB2B_Tests_`, aplica o snapshot, testa services/repositories e inicia a aplicação para testar HTTP/Razor/JSON. Ao terminar, mata somente o processo iniciado por ele e exclui somente seu banco temporário. Precisa de permissão para criar/excluir esse banco; não insere registros nas tabelas da conexão de origem. Para usar outro servidor, configure `LEADSCOPE_TEST_CONNECTION`. A execução também depende da configuração Debug do projeto web, construída automaticamente pelo comando acima. O [relatório de validação](Docs/Development/Validacao.md) registra os resultados e limites da verificação.

A correção explícita da dependência `System.Security.Cryptography.Pkcs` evita as vulnerabilidades transitivas da versão trazida por SqlClient 7.1.1. Referências técnicas: [SqlClient](https://github.com/dotnet/SqlClient/issues/4843), [cookies e APIs no ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0), [cálculo oficial do CNPJ alfanumérico](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj/manual-dv-cnpj.pdf).

## Configuração restante

Na base de desenvolvimento é possível cadastrar usuários comuns pela interface. Para administração, crie o primeiro administrador com uma senha escolhida pelo operador. Também faltam dados/fontes reais e chaves das aplicações autorizadas. A recuperação de senha por e-mail exige origem pública e SMTP reais; cadastro, login, perfil e senha autenticada funcionam sem essa configuração. A migração de versão de sessão já foi aplicada à base local. Adaptadores para serviços específicos, agendamento de coletas e importação de catálogos completos dependem de fontes e contratos ainda não definidos e não foram inventados.
