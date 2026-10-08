CREATE DATABASE LeadScopeB2B;
GO

USE LeadScopeB2B;
GO

-- =========================================================
-- TABELAS BASE
-- =========================================================

CREATE TABLE ConjuntoPermissoes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Descricao VARCHAR(255)
);

CREATE TABLE FonteDados (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Tipo VARCHAR(50),
    Url VARCHAR(255),
    Confiabilidade DECIMAL(5,2),
    DataHoraMod DATETIME2 DEFAULT SYSDATETIME(),
    DataHoraDel DATETIME2 NULL
);

CREATE TABLE PerfisAcesso (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    ConjuntoPermissoesId INT NOT NULL,
    CONSTRAINT FK_PerfisAcesso_ConjuntosPermissoes
        FOREIGN KEY (ConjuntoPermissoesId)
        REFERENCES ConjuntoPermissoes(Id)
);

CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Email VARCHAR(150) UNIQUE NOT NULL,
    Senha VARCHAR(255) NOT NULL,
    PerfilAcessoId INT NOT NULL,
    CONSTRAINT FK_Usuarios_PerfisAcesso
        FOREIGN KEY (PerfilAcessoId)
        REFERENCES PerfisAcesso(Id)
);

CREATE TABLE Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TiporEvento VARCHAR(50) NOT NULL,
    DescricaoEvento VARCHAR(MAX),
    DataHora DATETIME2 DEFAULT SYSDATETIME(),
    DadosAlterados VARCHAR(MAX),
    UsuarioId INT NOT NULL,
    CONSTRAINT FK_Logs_Usuarios
        FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios(Id)
);

CREATE TABLE Cnaes (
    Numero VARCHAR(20) PRIMARY KEY,
    Descricao VARCHAR(255) NOT NULL
);

CREATE TABLE Pessoas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(150) NOT NULL,
    Email VARCHAR(150),
    Telefone VARCHAR(20),
    UrlLinkedin VARCHAR(255),
    Status VARCHAR(50),
    Origem VARCHAR(100),
    DataColeta DATETIME2 DEFAULT SYSDATETIME(),
    DataBloqueio DATETIME2 NULL,
    DataDel DATETIME2 NULL
);

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
);

-- =========================================================
-- DADOS COLETADOS
-- =========================================================

CREATE TABLE DadosColetados (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Campo VARCHAR(100) NOT NULL,
    Valor VARCHAR(MAX) NOT NULL,
    NivelConfianca DECIMAL(5,2),
    FonteDadosID INT NOT NULL,
    PessoaId INT NULL,
    EmpresaId INT NULL,

    CONSTRAINT FK_DadosColetados_FontesDados
        FOREIGN KEY (FonteDadosID)
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
);

-- =========================================================
-- RELACIONAMENTOS N:N
-- =========================================================

CREATE TABLE CnaesEmpresas (
    CnaeNumero VARCHAR(20) NOT NULL,
    EmpresaId INT NOT NULL,
    CONSTRAINT PK_CnaesEmpresas PRIMARY KEY (CnaeNumero, EmpresaId),
    CONSTRAINT FK_CnaesEmpresas_Cnaes
        FOREIGN KEY (CnaeNumero)
        REFERENCES Cnaes(Numero),
    CONSTRAINT FK_CnaesEmpresas_Empresas
        FOREIGN KEY (EmpresaId)
        REFERENCES Empresas(Id)
);

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
    DataVerif DATETIME2,

    CONSTRAINT PK_PessoasEmpresas
        PRIMARY KEY (PessoaId, EmpresaId),

    CONSTRAINT FK_PessoasEmpresas_Pessoas
        FOREIGN KEY (PessoaId)
        REFERENCES Pessoas(Id),

    CONSTRAINT FK_PessoasEmpresas_Empresas
        FOREIGN KEY (EmpresaId)
        REFERENCES Empresas(Id)
);

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
);

CREATE TABLE Exportacoes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QtdItens INT,
    DataHora DATETIME2 DEFAULT SYSDATETIME(),
    UsuarioId INT NOT NULL,
    CONSTRAINT FK_Exportacoes_Usuarios
        FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios(Id)
);

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
);
GO
