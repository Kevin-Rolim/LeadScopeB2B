# Validação da aplicação

Referência: `Docs/Database/LeadScopeB2B.sql`, sem alterações no arquivo ou no banco existente.

## Verificações concluídas

- Compilação do projeto web e do executável de testes com .NET 10: zero erros e zero avisos.
- Inicialização HTTP: login, cadastro e recuperação responderam 200; API sem chave respondeu 401 em Problem Details. Essas verificações não dependem do banco.
- Verificação de contrato das 14 tabelas: nomes exatos das propriedades persistidas, tipos, nulabilidade e limites de tamanho compatíveis com as colunas do SQL definitivo.
- Remoção das referências a tabelas e colunas do esquema anterior nos repositories, models, serviços, controllers e views.
- Revisão dos testes para usar diretamente as definições do banco definitivo em um banco temporário, com os dados de referência de `InsertCPPA.sql`.

Para conferir o contrato sem conexão SQL Server:

```bash
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj -- --contrato
```

## Verificação dependente do ambiente

A suíte SQL/HTTP/SMTP não foi concluída nesta adaptação. A conexão de desenvolvimento salva aponta para `localhost,14330`, sem servidor acessível nessa porta. O SQL Server encontrado em `localhost,14331` recusou as credenciais disponíveis (erro 18456). As falhas ocorreram ao abrir a conexão, antes da criação de banco temporário ou de qualquer gravação.

Após configurar `LEADSCOPE_TEST_CONNECTION` com credenciais válidas para o servidor de testes:

```bash
dotnet run --project Tests/LeadScopeB2B.SmokeTests.csproj
```

A suíte cria e exclui apenas seu banco `LeadScopeB2B_Tests_<id>`; não utiliza as tabelas da base de origem. A conferência estática do contrato não substitui a execução dessa suíte contra SQL Server.
