SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.ConjuntoPermissoes
    WHERE Nome = N'Usuário'
)
BEGIN
    INSERT INTO dbo.ConjuntoPermissoes (Nome, Descricao)
    VALUES (N'Usuário', N'Permissões padrão dos usuários');
END;

DECLARE @ConjuntoId INT;

SELECT TOP (1) @ConjuntoId = Id
FROM dbo.ConjuntoPermissoes
WHERE Nome = N'Usuário'
ORDER BY Id;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.PerfisAcesso
    WHERE ConjuntoPermissoesId = @ConjuntoId
)
BEGIN
    INSERT INTO dbo.PerfisAcesso (Nome, ConjuntoPermissoesId)
    VALUES (N'Usuário', @ConjuntoId);
END;

COMMIT TRANSACTION;
