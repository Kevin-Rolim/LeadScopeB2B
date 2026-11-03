# Contrato do banco

A referência da aplicação é `Docs/Database/LeadScopeB2B.sql`, com os dados de referência em `Docs/Database/InsertCPPA.sql`. As 14 tabelas e suas colunas são utilizadas diretamente nos repositories e models. Não há detecção de versões, aliases de tabelas, renomeação ou alteração automática de estrutura.

`Usuarios` não possui status ou versão de sessão. `CnaesEmpresas` não possui indicação de principal. `DadosColetados` não possui estado de aprovação, data, evidência URL ou valor normalizado separado. `Empresas` não possui raiz do CNPJ nem índice filtrado para CNPJ nulo. `FonteDados` usa `DataHoraExclusao` para disponibilidade. A interface e os serviços respeitam essas estruturas.

Os scripts SQL fornecidos pelo operador não são executados durante o uso da aplicação. Testes usam somente um banco temporário independente.
