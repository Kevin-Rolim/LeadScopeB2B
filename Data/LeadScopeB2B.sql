-- LeadScopeB2B — ESQUEMA DEFINITIVO DA FACULDADE
-- 14 tabelas do modelo academico original; somente ajustes de NOMENCLATURA.
-- Nao adiciona campos, tabelas, relacionamentos ou migrations.
-- Execucao segura SOMENTE em banco vazio ou com as 14 tabelas originais SEM REGISTROS.
-- NUNCA aplicar ao banco principal/antigo. Banco de destino: LeadScopeB2B (Docker isolado).
-- Executar com sqlcmd -b -d LeadScopeB2B (nao use o script historico de insercoes).
-- AVISO: se houver linhas em qualquer tabela existente, aborta SEM apagar dados.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LeadScopeB2B'
    THROW 51000, 'Banco incorreto: conecte-se ao LeadScopeB2B.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Esperadas TABLE (Nome sysname PRIMARY KEY);
    INSERT INTO @Esperadas (Nome) VALUES
        (N'Cnaes'), (N'CnaesEmpresas'), (N'ConjuntoPermissoes'),
        (N'DadosColetados'), (N'Empresas'), (N'Exportacoes'),
        (N'ExportacoesPessoasEmpresas'), (N'FonteDados'), (N'Logs'),
        (N'PerfisAcesso'), (N'Pessoas'), (N'PessoasEmpresas'),
        (N'Revisoes'), (N'Usuarios');

    DECLARE @QuantidadeTabelas int = (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0);
    IF @QuantidadeTabelas NOT IN (0, 14)
        THROW 51001, 'Foram encontradas tabelas inesperadas ou o esquema esta incompleto. Nada alterado.', 1;

    IF EXISTS (
        SELECT 1 FROM sys.tables t
        WHERE t.is_ms_shipped = 0
          AND (SCHEMA_NAME(t.schema_id) <> N'dbo'
            OR NOT EXISTS (SELECT 1 FROM @Esperadas e WHERE e.Nome = t.name))
    )
        THROW 51002, 'Ha tabelas fora do modelo academico. Nada alterado.', 1;

    IF @QuantidadeTabelas = 14
    BEGIN
        -- Nao basta contar 14: TODAS devem corresponder ao modelo academico.
        IF EXISTS (
            SELECT 1 FROM @Esperadas e
            WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.Nome), N'U') IS NULL
        )
            THROW 51003, 'O conjunto de tabelas nao corresponde ao esquema academico. Nada alterado.', 1;

        -- Checagem real de linhas em cada tabela (nao baseada em estatisticas).
        DECLARE @Tabela sysname, @Consulta nvarchar(max), @TemDados bit;
        DECLARE cursor_tabelas CURSOR LOCAL FAST_FORWARD FOR SELECT Nome FROM @Esperadas;
        OPEN cursor_tabelas;
        FETCH NEXT FROM cursor_tabelas INTO @Tabela;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @TemDados = 0;
            SET @Consulta = N'SELECT @Resultado = CASE WHEN EXISTS(SELECT 1 FROM dbo.'
                    + QUOTENAME(@Tabela) + N') THEN 1 ELSE 0 END';
            EXEC sys.sp_executesql @Consulta, N'@Resultado bit OUTPUT', @Resultado = @TemDados OUTPUT;
            IF @TemDados = 1
                THROW 51004, 'O banco contem dados: recriacao bloqueada para evitar perdas.', 1;
            FETCH NEXT FROM cursor_tabelas INTO @Tabela;
        END;
        CLOSE cursor_tabelas;
        DEALLOCATE cursor_tabelas;

        -- Remocao SOMENTE das tabelas academicas vazias; ordem respeita as FKs.
        DROP TABLE dbo.ExportacoesPessoasEmpresas;
        DROP TABLE dbo.Revisoes;
        DROP TABLE dbo.CnaesEmpresas;
        DROP TABLE dbo.DadosColetados;
        DROP TABLE dbo.PessoasEmpresas;
        DROP TABLE dbo.Logs;
        DROP TABLE dbo.Exportacoes;
        DROP TABLE dbo.Usuarios;
        DROP TABLE dbo.PerfisAcesso;
        DROP TABLE dbo.ConjuntoPermissoes;
        DROP TABLE dbo.FonteDados;
        DROP TABLE dbo.Cnaes;
        DROP TABLE dbo.Pessoas;
        DROP TABLE dbo.Empresas;
    END;

    -- Definicoes oficiais: mesmas tabelas/colunas/tipos/FKs do script da faculdade.
CREATE TABLE dbo.ConjuntoPermissoes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Descricao VARCHAR(255)
);

CREATE TABLE dbo.FonteDados (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Tipo VARCHAR(50),
    Url VARCHAR(255),
    Confiabilidade DECIMAL(5,2),
    DataHoraModificacao DATETIME2 DEFAULT SYSDATETIME(),
    DataHoraExclusao DATETIME2 NULL
);

CREATE TABLE dbo.PerfisAcesso (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    ConjuntoPermissoesId INT NOT NULL,
    CONSTRAINT FK_PerfisAcesso_ConjuntosPermissoes
        FOREIGN KEY (ConjuntoPermissoesId)
        REFERENCES dbo.ConjuntoPermissoes(Id)
);

CREATE TABLE dbo.Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Email VARCHAR(150) UNIQUE NOT NULL,
    SenhaHash VARCHAR(255) NOT NULL,
    PerfilAcessoId INT NOT NULL,
    CONSTRAINT FK_Usuarios_PerfisAcesso
        FOREIGN KEY (PerfilAcessoId)
        REFERENCES dbo.PerfisAcesso(Id)
);

CREATE TABLE dbo.Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TipoEvento VARCHAR(50) NOT NULL,
    DescricaoEvento VARCHAR(MAX),
    DataHora DATETIME2 DEFAULT SYSDATETIME(),
    DadosAlterados VARCHAR(MAX),
    UsuarioId INT NOT NULL,
    CONSTRAINT FK_Logs_Usuarios
        FOREIGN KEY (UsuarioId)
        REFERENCES dbo.Usuarios(Id)
);

CREATE TABLE dbo.Cnaes (
    Numero VARCHAR(20) PRIMARY KEY,
    Descricao VARCHAR(255) NOT NULL
);

CREATE TABLE dbo.Pessoas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(150) NOT NULL,
    Email VARCHAR(150),
    Telefone VARCHAR(20),
    UrlLinkedIn VARCHAR(255),
    Status VARCHAR(50),
    Origem VARCHAR(100),
    DataColeta DATETIME2 DEFAULT SYSDATETIME(),
    DataBloqueio DATETIME2 NULL,
    DataExclusao DATETIME2 NULL
);

CREATE TABLE dbo.Empresas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RazaoSocial VARCHAR(200) NOT NULL,
    NomeFantasia VARCHAR(200),
    Cnpj VARCHAR(18) UNIQUE,
    UrlSite VARCHAR(255),
    Segmento VARCHAR(100),
    Porte VARCHAR(50),
    Cidade VARCHAR(100),
    Estado CHAR(2),
    Email VARCHAR(150),
    Telefone VARCHAR(20),
    Status VARCHAR(50),
    DataCriacao DATETIME2 DEFAULT SYSDATETIME(),
    DataAtualizacao DATETIME2 DEFAULT SYSDATETIME()
);

-- =========================================================
-- DADOS COLETADOS
-- =========================================================

CREATE TABLE dbo.DadosColetados (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Campo VARCHAR(100) NOT NULL,
    Valor VARCHAR(MAX) NOT NULL,
    NivelConfianca DECIMAL(5,2),
    FonteDadosId INT NOT NULL,
    PessoaId INT NULL,
    EmpresaId INT NULL,

    CONSTRAINT FK_DadosColetados_FonteDados
        FOREIGN KEY (FonteDadosId)
        REFERENCES dbo.FonteDados(Id),

    CONSTRAINT FK_DadosColetados_Pessoas
        FOREIGN KEY (PessoaId)
        REFERENCES dbo.Pessoas(Id),

    CONSTRAINT FK_DadosColetados_Empresas
        FOREIGN KEY (EmpresaId)
        REFERENCES dbo.Empresas(Id),

    CONSTRAINT CK_DadosColetados_EntidadeDestino
        CHECK (
            (PessoaId IS NOT NULL AND EmpresaId IS NULL)
            OR
            (PessoaId IS NULL AND EmpresaId IS NOT NULL)
        )
);

-- =========================================================
-- RELACIONAMENTOS N:N
-- =========================================================

CREATE TABLE dbo.CnaesEmpresas (
    CnaeNumero VARCHAR(20) NOT NULL,
    EmpresaId INT NOT NULL,
    CONSTRAINT PK_CnaesEmpresas PRIMARY KEY (CnaeNumero, EmpresaId),
    CONSTRAINT FK_CnaesEmpresas_Cnaes
        FOREIGN KEY (CnaeNumero)
        REFERENCES dbo.Cnaes(Numero),
    CONSTRAINT FK_CnaesEmpresas_Empresas
        FOREIGN KEY (EmpresaId)
        REFERENCES dbo.Empresas(Id)
);

CREATE TABLE dbo.PessoasEmpresas (
    PessoaId INT NOT NULL,
    EmpresaId INT NOT NULL,
    Cargo VARCHAR(100),
    Departamento VARCHAR(100),
    Senioridade VARCHAR(50),
    StatusVinculo VARCHAR(50),
    StatusLead VARCHAR(50),
    DataRevisao DATETIME2,
    NivelConfianca DECIMAL(5,2),
    FonteVinculo VARCHAR(100),
    DataVerificacao DATETIME2,

    CONSTRAINT PK_PessoasEmpresas
        PRIMARY KEY (PessoaId, EmpresaId),

    CONSTRAINT FK_PessoasEmpresas_Pessoas
        FOREIGN KEY (PessoaId)
        REFERENCES dbo.Pessoas(Id),

    CONSTRAINT FK_PessoasEmpresas_Empresas
        FOREIGN KEY (EmpresaId)
        REFERENCES dbo.Empresas(Id)
);

CREATE TABLE dbo.Revisoes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Decisao VARCHAR(100),
    Comentario VARCHAR(MAX),
    DataHora DATETIME2 DEFAULT SYSDATETIME(),
    UsuarioId INT NOT NULL,
    PessoaId INT NOT NULL,
    EmpresaId INT NOT NULL,

    CONSTRAINT FK_Revisoes_Usuarios
        FOREIGN KEY (UsuarioId)
        REFERENCES dbo.Usuarios(Id),

    CONSTRAINT FK_Revisoes_PessoasEmpresas
        FOREIGN KEY (PessoaId, EmpresaId)
        REFERENCES dbo.PessoasEmpresas(PessoaId, EmpresaId)
);

CREATE TABLE dbo.Exportacoes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QtdItens INT,
    DataHora DATETIME2 DEFAULT SYSDATETIME(),
    UsuarioId INT NOT NULL,
    CONSTRAINT FK_Exportacoes_Usuarios
        FOREIGN KEY (UsuarioId)
        REFERENCES dbo.Usuarios(Id)
);

CREATE TABLE dbo.ExportacoesPessoasEmpresas (
    ExportacoesId INT NOT NULL,
    PessoaId INT NOT NULL,
    EmpresaId INT NOT NULL,

    CONSTRAINT PK_ExportacoesPessoasEmpresas
        PRIMARY KEY (ExportacoesId, PessoaId, EmpresaId),

    CONSTRAINT FK_ExportacoesPE_Exportacoes
        FOREIGN KEY (ExportacoesId)
        REFERENCES dbo.Exportacoes(Id),

    CONSTRAINT FK_ExportacoesPE_PessoasEmpresas
        FOREIGN KEY (PessoaId, EmpresaId)
        REFERENCES dbo.PessoasEmpresas(PessoaId, EmpresaId)
);

    -- Somente dados de referencia para cadastro e autorizacao.
    -- Nao cria usuarios, senhas ficticias, empresas nem leads.
    INSERT INTO dbo.ConjuntoPermissoes (Nome, Descricao) VALUES
        ('Administrador', 'Acesso administrativo'),
        ('Revisor', 'Consulta e revisão de vínculos'),
        ('Usuário', 'Cadastro, pesquisa e exportação');

    INSERT INTO dbo.PerfisAcesso (Nome, ConjuntoPermissoesId)
        SELECT 'Administrador Geral', Id FROM dbo.ConjuntoPermissoes WHERE Nome = 'Administrador';
    INSERT INTO dbo.PerfisAcesso (Nome, ConjuntoPermissoesId)
        SELECT 'Revisor', Id FROM dbo.ConjuntoPermissoes WHERE Nome = 'Revisor';
    INSERT INTO dbo.PerfisAcesso (Nome, ConjuntoPermissoesId)
        SELECT 'Usuário', Id FROM dbo.ConjuntoPermissoes WHERE Nome = 'Usuário';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT COUNT(*) AS TotalTabelas FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo');
SELECT name AS Tabela FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') ORDER BY name;
PRINT 'Esquema academico definitivo aplicado. Nenhum usuario foi criado.';
