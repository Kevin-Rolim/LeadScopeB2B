# Diagnóstico inicial — 17/10/2026

A análise incluiu todos os controllers, services, repositories, models, view models e views, os scripts SQL, o modelo lógico, os diagramas de casos de uso/estados/BPMN, o DER e o esquema conectado via sys.tables/sys.columns/sys.indexes/sys.check_constraints. O PDF de dicionário foi renderizado e suas três páginas foram inspecionadas; confirma uma versão histórica intermediária, também divergente do banco atual (por exemplo senha versus senha_hash e email versus email_profissional).

- Build inicial: 9 erros (referências ao DbContext removido e contratos incompletos), além de avisos de dependência transitiva vulnerável.
- Migração iniciada pelo usuário: EF removido do csproj e DbContext excluído; três repositories ainda usam EF e o de empresas está incompleto. Services dependem do rastreamento implícito do EF.
- A chave de conexão da fábrica contém um erro de digitação: DefaultConnetion versus DefaultConnection.
- MVC: há telas CRUD parciais de pessoas, empresas, usuários e leads; faltam busca, CNAEs, revisão, fontes, auditoria e exportação. Não há autenticação, autorização ou endpoints de integração implementados.
- O banco conectado é a decisão mais recente: tabelas com sublinhados e colunas snake_case; lead tem chave composta pessoa_id/empresa_id. Há campos e índices ausentes nos scripts históricos. Nenhuma tabela será recriada ou relacionamento alterado.
- Banco inicial: 3 perfis e 3 conjuntos de permissões; demais tabelas vazias. Conjuntos reais: Administracao, Operacao Comercial e Leitura (diferentes do seed antigo Admin/Gestor/Leitura).
- Senha real usa senha_hash; usuários têm status ATIVO/BLOQUEADO/DESATIVADO. Pessoas têm exclusão lógica e bloqueio. Empresas têm status_validacao. CNAE é varchar(7) e principal é único por empresa. CNPJ numérico ou alfanumérico é varchar(14) com índice único filtrado; os exemplos antigos formatados são incompatíveis.
- Dados_coletados já tem status_validacao, valor_normalizado e evidencia_url e exige exatamente um destino existente. Revisoes pertence a um vínculo, não diretamente a Dados_coletados. A revisão de evidências deve usar Logs para auditoria, sem inventar relação nova.

A implementação preserva Controller → Service → Repository → SQL Server e os nomes públicos em português. Estados seguem os diagramas compatíveis com o banco; acesso segue os conjuntos reais, com aliases explícitos dos nomes do script histórico. Exclusão de empresas/leads/usuários será lógica para preservar revisão e exportação. Não serão criados conectores de terceiros.
