
USE LeadScopeB2B;

SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- =============================================
-- 1. CONJUNTOS DE PERMISSOES
-- =============================================

INSERT INTO ConjuntoPermissoes (Nome, Descricao)
SELECT Dados.Nome, Dados.Descricao
FROM (VALUES
    ('Administrador', 'Acesso completo ao sistema'),
    ('Revisor', 'Consulta e revisão de vínculos'),
    ('Usuário', 'Cadastro, pesquisa e exportação')
) AS Dados(Nome, Descricao)
WHERE NOT EXISTS (
    SELECT 1
    FROM ConjuntoPermissoes
    WHERE Nome = Dados.Nome
)

-- =============================================
-- 2. PERFIS DE ACESSO
-- =============================================

INSERT INTO PerfisAcesso (Nome, ConjuntoPermissoesId)
SELECT Dados.Nome, Permissoes.Id
FROM (VALUES
    ('Administrador Geral', 'Administrador'),
    ('Revisor', 'Revisor'),
    ('Usuário', 'Usuário')
) AS Dados(Nome, Conjunto)
INNER JOIN ConjuntoPermissoes AS Permissoes
    ON Permissoes.Nome = Dados.Conjunto
WHERE NOT EXISTS (
    SELECT 1
    FROM PerfisAcesso
    WHERE Nome = Dados.Nome
)

COMMIT TRANSACTION


-- =============================================
-- 3. VERIFICACAO
-- =============================================

SELECT
    Perfil.Id,
    Perfil.Nome AS Perfil,
    Permissao.Nome AS Permissao
FROM PerfisAcesso AS Perfil
INNER JOIN ConjuntoPermissoes AS Permissao
    ON Perfil.ConjuntoPermissoesId = Permissao.Id
ORDER BY Perfil.Id

