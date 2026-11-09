-- Executar uma vez em bases anteriores, antes de iniciar a versão acadêmica.
-- Mantém IDs, usuários, perfis personalizados e todos os relacionamentos.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE dbo.ConjuntoPermissoes SET Nome=N'Administrador', Descricao=N'Administração de usuários e auditoria' WHERE Nome='Admin';
UPDATE dbo.ConjuntoPermissoes SET Nome=N'Revisor', Descricao=N'Consulta e revisão de vínculos' WHERE Nome='Gestor';
UPDATE dbo.ConjuntoPermissoes SET Nome=N'Usuário', Descricao=N'Cadastro, pesquisa e exportação' WHERE Nome='Leitura';
UPDATE dbo.PerfisAcesso SET Nome=N'Revisor' WHERE Nome='Gestor Comercial';
UPDATE dbo.PerfisAcesso SET Nome=N'Usuário' WHERE Nome='Analista Comercial';
COMMIT;
