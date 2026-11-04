-- Validação não destrutiva do modelo acadêmico: 14 tabelas e 84 colunas.
-- Usado pelo inicializador Docker em bancos novos ou preexistentes.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LeadScopeB2B'
    THROW 51100, 'Banco incorreto para validação do esquema.', 1;

DECLARE @Tabelas TABLE (Nome sysname PRIMARY KEY);
INSERT INTO @Tabelas (Nome) VALUES
    (N'ConjuntoPermissoes'),
    (N'FonteDados'),
    (N'PerfisAcesso'),
    (N'Usuarios'),
    (N'Logs'),
    (N'Cnaes'),
    (N'Pessoas'),
    (N'Empresas'),
    (N'DadosColetados'),
    (N'CnaesEmpresas'),
    (N'PessoasEmpresas'),
    (N'Revisoes'),
    (N'Exportacoes'),
    (N'ExportacoesPessoasEmpresas');

IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) <> 14
    OR EXISTS (
        SELECT 1 FROM @Tabelas e
        WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.Nome), N'U') IS NULL
    )
    OR EXISTS (
        SELECT 1 FROM sys.tables t
        WHERE t.is_ms_shipped = 0
          AND (SCHEMA_NAME(t.schema_id) <> N'dbo'
               OR NOT EXISTS (SELECT 1 FROM @Tabelas e WHERE e.Nome = t.name))
    )
    THROW 51101, 'Esquema incompatível: esperado exatamente o conjunto acadêmico de 14 tabelas em dbo. Nenhum dado foi alterado.', 1;

DECLARE @Colunas TABLE (
    Tabela sysname NOT NULL,
    Coluna sysname NOT NULL,
    Tipo sysname NOT NULL,
    Tamanho smallint NULL,
    Precisao tinyint NULL,
    Escala tinyint NULL,
    AceitaNulo bit NOT NULL,
    PRIMARY KEY (Tabela, Coluna)
);
INSERT INTO @Colunas VALUES
    (N'ConjuntoPermissoes', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'ConjuntoPermissoes', N'Nome', N'varchar', 100, NULL, NULL, 0),
    (N'ConjuntoPermissoes', N'Descricao', N'varchar', 255, NULL, NULL, 1),
    (N'FonteDados', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'FonteDados', N'Nome', N'varchar', 100, NULL, NULL, 0),
    (N'FonteDados', N'Tipo', N'varchar', 50, NULL, NULL, 1),
    (N'FonteDados', N'Url', N'varchar', 255, NULL, NULL, 1),
    (N'FonteDados', N'Confiabilidade', N'decimal', NULL, 5, 2, 1),
    (N'FonteDados', N'DataHoraModificacao', N'datetime2', NULL, NULL, NULL, 1),
    (N'FonteDados', N'DataHoraExclusao', N'datetime2', NULL, NULL, NULL, 1),
    (N'PerfisAcesso', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'PerfisAcesso', N'Nome', N'varchar', 100, NULL, NULL, 0),
    (N'PerfisAcesso', N'ConjuntoPermissoesId', N'int', NULL, NULL, NULL, 0),
    (N'Usuarios', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Usuarios', N'Nome', N'varchar', 100, NULL, NULL, 0),
    (N'Usuarios', N'Email', N'varchar', 150, NULL, NULL, 0),
    (N'Usuarios', N'SenhaHash', N'varchar', 255, NULL, NULL, 0),
    (N'Usuarios', N'PerfilAcessoId', N'int', NULL, NULL, NULL, 0),
    (N'Logs', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Logs', N'TipoEvento', N'varchar', 50, NULL, NULL, 0),
    (N'Logs', N'DescricaoEvento', N'varchar', -1, NULL, NULL, 1),
    (N'Logs', N'DataHora', N'datetime2', NULL, NULL, NULL, 1),
    (N'Logs', N'DadosAlterados', N'varchar', -1, NULL, NULL, 1),
    (N'Logs', N'UsuarioId', N'int', NULL, NULL, NULL, 0),
    (N'Cnaes', N'Numero', N'varchar', 20, NULL, NULL, 0),
    (N'Cnaes', N'Descricao', N'varchar', 255, NULL, NULL, 0),
    (N'Pessoas', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Pessoas', N'Nome', N'varchar', 150, NULL, NULL, 0),
    (N'Pessoas', N'Email', N'varchar', 150, NULL, NULL, 1),
    (N'Pessoas', N'Telefone', N'varchar', 20, NULL, NULL, 1),
    (N'Pessoas', N'UrlLinkedIn', N'varchar', 255, NULL, NULL, 1),
    (N'Pessoas', N'Status', N'varchar', 50, NULL, NULL, 1),
    (N'Pessoas', N'Origem', N'varchar', 100, NULL, NULL, 1),
    (N'Pessoas', N'DataColeta', N'datetime2', NULL, NULL, NULL, 1),
    (N'Pessoas', N'DataBloqueio', N'datetime2', NULL, NULL, NULL, 1),
    (N'Pessoas', N'DataExclusao', N'datetime2', NULL, NULL, NULL, 1),
    (N'Empresas', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Empresas', N'RazaoSocial', N'varchar', 200, NULL, NULL, 0),
    (N'Empresas', N'NomeFantasia', N'varchar', 200, NULL, NULL, 1),
    (N'Empresas', N'Cnpj', N'varchar', 18, NULL, NULL, 1),
    (N'Empresas', N'UrlSite', N'varchar', 255, NULL, NULL, 1),
    (N'Empresas', N'Segmento', N'varchar', 100, NULL, NULL, 1),
    (N'Empresas', N'Porte', N'varchar', 50, NULL, NULL, 1),
    (N'Empresas', N'Cidade', N'varchar', 100, NULL, NULL, 1),
    (N'Empresas', N'Estado', N'char', 2, NULL, NULL, 1),
    (N'Empresas', N'Email', N'varchar', 150, NULL, NULL, 1),
    (N'Empresas', N'Telefone', N'varchar', 20, NULL, NULL, 1),
    (N'Empresas', N'Status', N'varchar', 50, NULL, NULL, 1),
    (N'Empresas', N'DataCriacao', N'datetime2', NULL, NULL, NULL, 1),
    (N'Empresas', N'DataAtualizacao', N'datetime2', NULL, NULL, NULL, 1),
    (N'DadosColetados', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'DadosColetados', N'Campo', N'varchar', 100, NULL, NULL, 0),
    (N'DadosColetados', N'Valor', N'varchar', -1, NULL, NULL, 0),
    (N'DadosColetados', N'NivelConfianca', N'decimal', NULL, 5, 2, 1),
    (N'DadosColetados', N'FonteDadosId', N'int', NULL, NULL, NULL, 0),
    (N'DadosColetados', N'PessoaId', N'int', NULL, NULL, NULL, 1),
    (N'DadosColetados', N'EmpresaId', N'int', NULL, NULL, NULL, 1),
    (N'CnaesEmpresas', N'CnaeNumero', N'varchar', 20, NULL, NULL, 0),
    (N'CnaesEmpresas', N'EmpresaId', N'int', NULL, NULL, NULL, 0),
    (N'PessoasEmpresas', N'PessoaId', N'int', NULL, NULL, NULL, 0),
    (N'PessoasEmpresas', N'EmpresaId', N'int', NULL, NULL, NULL, 0),
    (N'PessoasEmpresas', N'Cargo', N'varchar', 100, NULL, NULL, 1),
    (N'PessoasEmpresas', N'Departamento', N'varchar', 100, NULL, NULL, 1),
    (N'PessoasEmpresas', N'Senioridade', N'varchar', 50, NULL, NULL, 1),
    (N'PessoasEmpresas', N'StatusVinculo', N'varchar', 50, NULL, NULL, 1),
    (N'PessoasEmpresas', N'StatusLead', N'varchar', 50, NULL, NULL, 1),
    (N'PessoasEmpresas', N'DataRevisao', N'datetime2', NULL, NULL, NULL, 1),
    (N'PessoasEmpresas', N'NivelConfianca', N'decimal', NULL, 5, 2, 1),
    (N'PessoasEmpresas', N'FonteVinculo', N'varchar', 100, NULL, NULL, 1),
    (N'PessoasEmpresas', N'DataVerificacao', N'datetime2', NULL, NULL, NULL, 1),
    (N'Revisoes', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Revisoes', N'Decisao', N'varchar', 100, NULL, NULL, 1),
    (N'Revisoes', N'Comentario', N'varchar', -1, NULL, NULL, 1),
    (N'Revisoes', N'DataHora', N'datetime2', NULL, NULL, NULL, 1),
    (N'Revisoes', N'UsuarioId', N'int', NULL, NULL, NULL, 0),
    (N'Revisoes', N'PessoaId', N'int', NULL, NULL, NULL, 0),
    (N'Revisoes', N'EmpresaId', N'int', NULL, NULL, NULL, 0),
    (N'Exportacoes', N'Id', N'int', NULL, NULL, NULL, 0),
    (N'Exportacoes', N'QtdItens', N'int', NULL, NULL, NULL, 1),
    (N'Exportacoes', N'DataHora', N'datetime2', NULL, NULL, NULL, 1),
    (N'Exportacoes', N'UsuarioId', N'int', NULL, NULL, NULL, 0),
    (N'ExportacoesPessoasEmpresas', N'ExportacoesId', N'int', NULL, NULL, NULL, 0),
    (N'ExportacoesPessoasEmpresas', N'PessoaId', N'int', NULL, NULL, NULL, 0),
    (N'ExportacoesPessoasEmpresas', N'EmpresaId', N'int', NULL, NULL, NULL, 0);

IF EXISTS (
    SELECT 1
    FROM @Colunas e
    JOIN sys.tables t ON t.name = e.Tabela AND SCHEMA_NAME(t.schema_id) = N'dbo'
    LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = e.Coluna
    LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.column_id IS NULL
       OR ty.name <> e.Tipo
       OR c.is_nullable <> e.AceitaNulo
       OR (e.Tamanho IS NOT NULL AND c.max_length <> e.Tamanho)
       OR (e.Precisao IS NOT NULL AND c.precision <> e.Precisao)
       OR (e.Escala IS NOT NULL AND c.scale <> e.Escala)
)
OR EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.is_ms_shipped = 0 AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND NOT EXISTS (
          SELECT 1 FROM @Colunas e WHERE e.Tabela = t.name AND e.Coluna = c.name
      )
)
    THROW 51102, 'Colunas, tipos ou nulabilidade incompatíveis com o esquema acadêmico. Nenhum dado foi alterado.', 1;

IF (SELECT COUNT(*) FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id WHERE k.type='PK' AND SCHEMA_NAME(t.schema_id)=N'dbo') <> 14
   OR (SELECT COUNT(*) FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id WHERE SCHEMA_NAME(t.schema_id)=N'dbo') <> 15
   OR OBJECT_ID(N'dbo.DadosColetados', N'U') IS NULL
   OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.DadosColetados') AND name=N'CK_DadosColetados_EntidadeDestino')
    THROW 51103, 'Restrições de integridade incompatíveis (PK/FK/CHECK). Nenhum dado foi alterado.', 1;

IF EXISTS (
    SELECT 1 FROM (VALUES (N'Administrador'), (N'Revisor'), (N'Usuário')) papel(Nome)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.PerfisAcesso p
        JOIN dbo.ConjuntoPermissoes c ON c.Id = p.ConjuntoPermissoesId
        WHERE c.Nome = papel.Nome
    )
)
    THROW 51104, 'Perfis acadêmicos ausentes ou legados. Em banco antigo, avalie Data/AdequarPerfis.sql antes de reiniciar. Nenhum dado foi alterado.', 1;

PRINT 'Banco LeadScopeB2B validado: 14 tabelas, 84 colunas, integridade e perfis. Dados existentes preservados.';
