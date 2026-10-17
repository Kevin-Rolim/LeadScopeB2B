-- LeadScopeB2B: revogação persistente de sessões e links de recuperação.
-- Aplicar uma vez em cada instalação existente; pode ser executado novamente.
-- Preserva todas as linhas, hashes, perfis, índices e relacionamentos existentes.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
    THROW 51000, 'A tabela dbo.Usuarios não existe no banco selecionado.', 1;

IF COL_LENGTH(N'dbo.Usuarios', N'versao_sessao') IS NULL
    ALTER TABLE dbo.Usuarios ADD versao_sessao BIGINT NOT NULL
        CONSTRAINT DF_Usuarios_VersaoSessao DEFAULT (0) WITH VALUES;
ELSE IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'versao_sessao'
      AND system_type_id = TYPE_ID(N'bigint') AND is_nullable = 0
)
    THROW 51001, 'versao_sessao existente tem um tipo incompatível; nenhuma alteração foi aplicada.', 1;
COMMIT TRANSACTION;
