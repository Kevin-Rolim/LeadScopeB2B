CREATE DATABASE LeadScopeB2B;
GO

USE LeadScopeB2B;
GO

-- =========================================================
-- TABELAS BASE
-- =========================================================

CREATE TABLE Conjuntos_permissoes (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    descricao VARCHAR(255)
);

CREATE TABLE Fontes_dados (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    tipo VARCHAR(50),
    url VARCHAR(255),
    confiabilidade DECIMAL(5,2),
    data_hora_mod DATETIME2 DEFAULT SYSDATETIME(),
    data_hora_del DATETIME2 NULL
);

CREATE TABLE Perfis_acesso (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    conjunto_permissoes_id INT NOT NULL,
    CONSTRAINT FK_PerfisAcesso_ConjuntosPermissoes
        FOREIGN KEY (conjunto_permissoes_id)
        REFERENCES Conjuntos_permissoes(id)
);

CREATE TABLE Usuarios (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    email VARCHAR(150) UNIQUE NOT NULL,
    senha VARCHAR(255) NOT NULL,
    perfil_acesso_id INT NOT NULL,
    CONSTRAINT FK_Usuarios_PerfisAcesso
        FOREIGN KEY (perfil_acesso_id)
        REFERENCES Perfis_acesso(id)
);

CREATE TABLE Logs (
    id INT IDENTITY(1,1) PRIMARY KEY,
    tipo_evento VARCHAR(50) NOT NULL,
    desc_evento VARCHAR(MAX),
    data_hora DATETIME2 DEFAULT SYSDATETIME(),
    dados_alt VARCHAR(MAX),
    usuario_id INT NOT NULL,
    CONSTRAINT FK_Logs_Usuarios
        FOREIGN KEY (usuario_id)
        REFERENCES Usuarios(id)
);

CREATE TABLE CNAEs (
    numero VARCHAR(20) PRIMARY KEY,
    descricao VARCHAR(255) NOT NULL
);

CREATE TABLE Pessoas (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nome VARCHAR(150) NOT NULL,
    email VARCHAR(150),
    telefone VARCHAR(20),
    url_linkedin VARCHAR(255),
    status VARCHAR(50),
    origem VARCHAR(100),
    data_coleta DATETIME2 DEFAULT SYSDATETIME(),
    data_bloqueio DATETIME2 NULL,
    data_del DATETIME2 NULL
);

CREATE TABLE Empresas (
    id INT IDENTITY(1,1) PRIMARY KEY,
    razao_social VARCHAR(200) NOT NULL,
    nome_fantasia VARCHAR(200),
    cnpj VARCHAR(18) UNIQUE,
    site VARCHAR(255),
    segmento VARCHAR(100),
    porte VARCHAR(50),
    cidade VARCHAR(100),
    estado CHAR(2),
    email VARCHAR(150),
    telefone VARCHAR(20),
    status VARCHAR(50),
    data_criacao DATETIME2 DEFAULT SYSDATETIME(),
    data_atualizacao DATETIME2 DEFAULT SYSDATETIME()
);

-- =========================================================
-- DADOS COLETADOS
-- =========================================================

CREATE TABLE Dados_coletados (
    id INT IDENTITY(1,1) PRIMARY KEY,
    campo VARCHAR(100) NOT NULL,
    valor VARCHAR(MAX) NOT NULL,
    nivel_confianca DECIMAL(5,2),
    fonte_dados_id INT NOT NULL,
    pessoa_id INT NULL,
    empresa_id INT NULL,

    CONSTRAINT FK_DadosColetados_FontesDados
        FOREIGN KEY (fonte_dados_id)
        REFERENCES Fontes_dados(id),

    CONSTRAINT FK_DadosColetados_Pessoas
        FOREIGN KEY (pessoa_id)
        REFERENCES Pessoas(id),

    CONSTRAINT FK_DadosColetados_Empresas
        FOREIGN KEY (empresa_id)
        REFERENCES Empresas(id),

    CONSTRAINT CK_DadosColetados_EntidadeDestino
        CHECK (
            (pessoa_id IS NOT NULL AND empresa_id IS NULL)
            OR
            (pessoa_id IS NULL AND empresa_id IS NOT NULL)
        )
);

-- =========================================================
-- RELACIONAMENTOS N:N
-- =========================================================

CREATE TABLE CNAEs_Empresas (
    cnae_numero VARCHAR(20) NOT NULL,
    empresa_id INT NOT NULL,
    CONSTRAINT PK_CNAEs_Empresas PRIMARY KEY (cnae_numero, empresa_id),
    CONSTRAINT FK_CNAEsEmpresas_CNAEs
        FOREIGN KEY (cnae_numero)
        REFERENCES CNAEs(numero),
    CONSTRAINT FK_CNAEsEmpresas_Empresas
        FOREIGN KEY (empresa_id)
        REFERENCES Empresas(id)
);

CREATE TABLE Pessoas_Empresas (
    pessoa_id INT NOT NULL,
    empresa_id INT NOT NULL,
    cargo VARCHAR(100),
    departamento VARCHAR(100),
    senioridade VARCHAR(50),
    status_vinculo VARCHAR(50),
    status_lead VARCHAR(50),
    data_revisao DATETIME2,
    nivel_confianca DECIMAL(5,2),
    fonte_vinculo VARCHAR(100),
    data_verificacao DATETIME2,

    CONSTRAINT PK_Pessoas_Empresas
        PRIMARY KEY (pessoa_id, empresa_id),

    CONSTRAINT FK_PessoasEmpresas_Pessoas
        FOREIGN KEY (pessoa_id)
        REFERENCES Pessoas(id),

    CONSTRAINT FK_PessoasEmpresas_Empresas
        FOREIGN KEY (empresa_id)
        REFERENCES Empresas(id)
);

CREATE TABLE Revisoes (
    id INT IDENTITY(1,1) PRIMARY KEY,
    decisao VARCHAR(100),
    comentario VARCHAR(MAX),
    data_hora DATETIME2 DEFAULT SYSDATETIME(),
    usuario_id INT NOT NULL,
    pessoa_id INT NOT NULL,
    empresa_id INT NOT NULL,

    CONSTRAINT FK_Revisoes_Usuarios
        FOREIGN KEY (usuario_id)
        REFERENCES Usuarios(id),

    CONSTRAINT FK_Revisoes_PessoasEmpresas
        FOREIGN KEY (pessoa_id, empresa_id)
        REFERENCES Pessoas_Empresas(pessoa_id, empresa_id)
);

CREATE TABLE Exportacoes (
    id INT IDENTITY(1,1) PRIMARY KEY,
    qntd_itens INT,
    data_hora DATETIME2 DEFAULT SYSDATETIME(),
    usuario_id INT NOT NULL,
    CONSTRAINT FK_Exportacoes_Usuarios
        FOREIGN KEY (usuario_id)
        REFERENCES Usuarios(id)
);

CREATE TABLE Exportacoes_Pessoas_Empresas (
    exportacao_id INT NOT NULL,
    pessoa_id INT NOT NULL,
    empresa_id INT NOT NULL,

    CONSTRAINT PK_Exportacoes_Pessoas_Empresas
        PRIMARY KEY (exportacao_id, pessoa_id, empresa_id),

    CONSTRAINT FK_ExportacoesPE_Exportacoes
        FOREIGN KEY (exportacao_id)
        REFERENCES Exportacoes(id),

    CONSTRAINT FK_ExportacoesPE_PessoasEmpresas
        FOREIGN KEY (pessoa_id, empresa_id)
        REFERENCES Pessoas_Empresas(pessoa_id, empresa_id)
);
GO
