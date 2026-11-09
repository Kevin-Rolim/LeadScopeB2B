USE LeadScopeB2B
GO

SET NOCOUNT ON
SET XACT_ABORT ON

BEGIN TRY
    BEGIN TRANSACTION

    -- =====================================================
    -- CONJUNTOS DE PERMISSOES
    -- =====================================================

    CREATE TABLE ConjuntoPermissoes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nome VARCHAR(100) NOT NULL,
        Descricao VARCHAR(255)
    )

    -- =====================================================
    -- FONTES DE DADOS
    -- =====================================================

    CREATE TABLE FonteDados (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nome VARCHAR(100) NOT NULL,
        Tipo VARCHAR(50),
        Url VARCHAR(255),
        Confiabilidade DECIMAL(5,2),
        DataHoraModificacao DATETIME2 DEFAULT SYSDATETIME(),
        DataHoraExclusao DATETIME2 NULL
    )

    -- =====================================================
    -- PERFIS DE ACESSO
    -- =====================================================

    CREATE TABLE PerfisAcesso (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nome VARCHAR(100) NOT NULL,
        ConjuntoPermissoesId INT NOT NULL,

        CONSTRAINT FK_PerfisAcesso_ConjuntoPermissoes
            FOREIGN KEY (ConjuntoPermissoesId)
            REFERENCES ConjuntoPermissoes(Id)
    )

    -- =====================================================
    -- USUARIOS
    -- =====================================================

    CREATE TABLE Usuarios (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nome VARCHAR(100) NOT NULL,
        Email VARCHAR(150) UNIQUE NOT NULL,
        SenhaHash VARCHAR(255) NOT NULL,
        PerfilAcessoId INT NOT NULL,

        CONSTRAINT FK_Usuarios_PerfisAcesso
            FOREIGN KEY (PerfilAcessoId)
            REFERENCES PerfisAcesso(Id)
    )

    -- =====================================================
    -- LOGS
    -- =====================================================

    CREATE TABLE Logs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TipoEvento VARCHAR(50) NOT NULL,
        DescricaoEvento VARCHAR(MAX),
        DataHora DATETIME2 DEFAULT SYSDATETIME(),
        DadosAlterados VARCHAR(MAX),
        UsuarioId INT NOT NULL,

        CONSTRAINT FK_Logs_Usuarios
            FOREIGN KEY (UsuarioId)
            REFERENCES Usuarios(Id)
    )

    -- =====================================================
    -- CNAES
    -- =====================================================

    CREATE TABLE Cnaes (
        Numero VARCHAR(20) PRIMARY KEY,
        Descricao VARCHAR(255) NOT NULL
    )

    -- =====================================================
    -- PESSOAS
    -- =====================================================

    CREATE TABLE Pessoas (
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
    )

    -- =====================================================
    -- EMPRESAS
    -- =====================================================

    CREATE TABLE Empresas (
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
    )

    -- =====================================================
    -- DADOS COLETADOS
    -- =====================================================

    CREATE TABLE DadosColetados (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Campo VARCHAR(100) NOT NULL,
        Valor VARCHAR(MAX) NOT NULL,
        NivelConfianca DECIMAL(5,2),
        FonteDadosId INT NOT NULL,
        PessoaId INT NULL,
        EmpresaId INT NULL,

        CONSTRAINT FK_DadosColetados_FonteDados
            FOREIGN KEY (FonteDadosId)
            REFERENCES FonteDados(Id),

        CONSTRAINT FK_DadosColetados_Pessoas
            FOREIGN KEY (PessoaId)
            REFERENCES Pessoas(Id),

        CONSTRAINT FK_DadosColetados_Empresas
            FOREIGN KEY (EmpresaId)
            REFERENCES Empresas(Id),

        CONSTRAINT CK_DadosColetados_EntidadeDestino
            CHECK (
                (PessoaId IS NOT NULL AND EmpresaId IS NULL)
                OR
                (PessoaId IS NULL AND EmpresaId IS NOT NULL)
            )
    )

    -- =====================================================
    -- CNAES E EMPRESAS
    -- =====================================================

    CREATE TABLE CnaesEmpresas (
        CnaeNumero VARCHAR(20) NOT NULL,
        EmpresaId INT NOT NULL,

        CONSTRAINT PK_CnaesEmpresas
            PRIMARY KEY (CnaeNumero, EmpresaId),

        CONSTRAINT FK_CnaesEmpresas_Cnaes
            FOREIGN KEY (CnaeNumero)
            REFERENCES Cnaes(Numero),

        CONSTRAINT FK_CnaesEmpresas_Empresas
            FOREIGN KEY (EmpresaId)
            REFERENCES Empresas(Id)
    )

    -- =====================================================
    -- PESSOAS E EMPRESAS
    -- =====================================================

    CREATE TABLE PessoasEmpresas (
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
            REFERENCES Pessoas(Id),

        CONSTRAINT FK_PessoasEmpresas_Empresas
            FOREIGN KEY (EmpresaId)
            REFERENCES Empresas(Id)
    )

    -- =====================================================
    -- REVISOES
    -- =====================================================

    CREATE TABLE Revisoes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Decisao VARCHAR(100),
        Comentario VARCHAR(MAX),
        DataHora DATETIME2 DEFAULT SYSDATETIME(),
        UsuarioId INT NOT NULL,
        PessoaId INT NOT NULL,
        EmpresaId INT NOT NULL,

        CONSTRAINT FK_Revisoes_Usuarios
            FOREIGN KEY (UsuarioId)
            REFERENCES Usuarios(Id),

        CONSTRAINT FK_Revisoes_PessoasEmpresas
            FOREIGN KEY (PessoaId, EmpresaId)
            REFERENCES PessoasEmpresas(PessoaId, EmpresaId)
    )

    -- =====================================================
    -- EXPORTACOES
    -- =====================================================

    CREATE TABLE Exportacoes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        QtdItens INT,
        DataHora DATETIME2 DEFAULT SYSDATETIME(),
        UsuarioId INT NOT NULL,

        CONSTRAINT FK_Exportacoes_Usuarios
            FOREIGN KEY (UsuarioId)
            REFERENCES Usuarios(Id)
    )

    -- =====================================================
    -- EXPORTACOES DE PESSOAS E EMPRESAS
    -- =====================================================

    CREATE TABLE ExportacoesPessoasEmpresas (
        ExportacoesId INT NOT NULL,
        PessoaId INT NOT NULL,
        EmpresaId INT NOT NULL,

        CONSTRAINT PK_ExportacoesPessoasEmpresas
            PRIMARY KEY (ExportacoesId, PessoaId, EmpresaId),

        CONSTRAINT FK_ExportacoesPE_Exportacoes
            FOREIGN KEY (ExportacoesId)
            REFERENCES Exportacoes(Id),

        CONSTRAINT FK_ExportacoesPE_PessoasEmpresas
            FOREIGN KEY (PessoaId, EmpresaId)
            REFERENCES PessoasEmpresas(PessoaId, EmpresaId)
    )

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW
END CATCH
