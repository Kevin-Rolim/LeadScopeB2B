-- Snapshot do esquema dbo consultado em 17/10/2026. Sem dados operacionais.
-- Execute somente em banco NOVO e VAZIO, escolhido explicitamente pelo operador.
-- Não é migração: não executar contra uma instalação existente.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE [dbo].[CNAEs] (
    [numero] varchar(7) NOT NULL,
    [descricao] nvarchar(300) NOT NULL
 );
GO

CREATE TABLE [dbo].[CNAEs_Empresas] (
    [cnae_numero] varchar(7) NOT NULL,
    [empresa_id] int NOT NULL,
    [principal] bit NOT NULL DEFAULT ((0))
 );
GO

CREATE TABLE [dbo].[Conjuntos_permissoes] (
    [id] int IDENTITY(1,1) NOT NULL,
    [nome] nvarchar(100) NOT NULL,
    [descricao] nvarchar(500) NULL
 );
GO

CREATE TABLE [dbo].[Dados_coletados] (
    [id] int IDENTITY(1,1) NOT NULL,
    [campo] varchar(120) NOT NULL,
    [valor] nvarchar(MAX) NOT NULL,
    [valor_normalizado] nvarchar(MAX) NULL,
    [nivel_confianca] decimal(5,2) NULL,
    [status_validacao] varchar(30) NULL,
    [data_coleta] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [evidencia_url] nvarchar(2048) NULL,
    [fonte_dados_id] int NOT NULL,
    [pessoa_id] int NULL,
    [empresa_id] int NULL
 );
GO

CREATE TABLE [dbo].[Empresas] (
    [id] int IDENTITY(1,1) NOT NULL,
    [razao_social] nvarchar(300) NOT NULL,
    [nome_fantasia] nvarchar(250) NULL,
    [cnpj] varchar(14) NULL,
    [cnpj_raiz] varchar(8) NULL,
    [tipo_unidade] varchar(10) NULL,
    [dominio_site] varchar(253) NULL,
    [site] nvarchar(1000) NULL,
    [descricao] nvarchar(2000) NULL,
    [segmento] nvarchar(150) NULL,
    [porte] varchar(50) NULL,
    [qtd_colaboradores_aprox] int NULL,
    [cep] varchar(12) NULL,
    [logradouro] nvarchar(250) NULL,
    [numero] nvarchar(30) NULL,
    [complemento] nvarchar(250) NULL,
    [bairro] nvarchar(150) NULL,
    [cidade] nvarchar(150) NULL,
    [estado] nvarchar(100) NULL,
    [pais] char(2) NULL,
    [email_institucional] nvarchar(254) NULL,
    [telefone_comercial] varchar(30) NULL,
    [situacao_cadastral] varchar(50) NULL,
    [status_validacao] varchar(30) NOT NULL DEFAULT ('PENDENTE'),
    [data_inicio_atividade] date NULL,
    [data_criacao] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [data_atualizacao] datetime2(0) NOT NULL DEFAULT (sysutcdatetime())
 );
GO

CREATE TABLE [dbo].[Exportacoes] (
    [id] int IDENTITY(1,1) NOT NULL,
    [qntd_itens] int NOT NULL DEFAULT ((0)),
    [formato] varchar(20) NOT NULL DEFAULT ('CSV'),
    [filtros] nvarchar(MAX) NULL,
    [data_hora] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [usuario_id] int NOT NULL
 );
GO

CREATE TABLE [dbo].[Exportacoes_Pessoas_Empresas] (
    [exportacao_id] int NOT NULL,
    [pessoa_id] int NOT NULL,
    [empresa_id] int NOT NULL
 );
GO

CREATE TABLE [dbo].[Fontes_dados] (
    [id] int IDENTITY(1,1) NOT NULL,
    [nome] nvarchar(150) NOT NULL,
    [tipo] varchar(30) NOT NULL,
    [descricao] nvarchar(1000) NULL,
    [url] nvarchar(1000) NULL,
    [confiabilidade] decimal(5,2) NULL,
    [limite_requisicoes] int NULL,
    [requer_autenticacao] bit NOT NULL DEFAULT ((0)),
    [ativo] bit NOT NULL DEFAULT ((1)),
    [data_hora_mod] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [data_hora_del] datetime2(0) NULL
 );
GO

CREATE TABLE [dbo].[Logs] (
    [id] int IDENTITY(1,1) NOT NULL,
    [tipo_evento] varchar(80) NOT NULL,
    [desc_evento] nvarchar(MAX) NULL,
    [data_hora] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [dados_alt] nvarchar(MAX) NULL,
    [usuario_id] int NOT NULL
 );
GO

CREATE TABLE [dbo].[Perfis_acesso] (
    [id] int IDENTITY(1,1) NOT NULL,
    [nome] nvarchar(100) NOT NULL,
    [conjunto_permissoes_id] int NOT NULL
 );
GO

CREATE TABLE [dbo].[Pessoas] (
    [id] int IDENTITY(1,1) NOT NULL,
    [nome] nvarchar(200) NOT NULL,
    [email_profissional] nvarchar(254) NULL,
    [telefone_comercial] varchar(30) NULL,
    [url_perfil_profissional] nvarchar(1000) NULL,
    [cidade] nvarchar(150) NULL,
    [estado] nvarchar(100) NULL,
    [pais] char(2) NULL,
    [status_validacao] varchar(30) NOT NULL DEFAULT ('PENDENTE'),
    [origem] nvarchar(150) NULL,
    [data_coleta] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [data_verificacao] datetime2(0) NULL,
    [data_bloqueio] datetime2(0) NULL,
    [data_del] datetime2(0) NULL
 );
GO

CREATE TABLE [dbo].[Pessoas_Empresas] (
    [pessoa_id] int NOT NULL,
    [empresa_id] int NOT NULL,
    [cargo] nvarchar(200) NULL,
    [departamento] nvarchar(120) NULL,
    [senioridade] varchar(50) NULL,
    [tipo_vinculo] varchar(50) NULL,
    [qualificacao_formal] nvarchar(200) NULL,
    [status_vinculo] varchar(50) NULL,
    [status_lead] varchar(50) NULL,
    [data_revisao] datetime2(0) NULL,
    [nivel_confianca] decimal(5,2) NULL,
    [fonte_vinculo] nvarchar(150) NULL,
    [data_verificacao] datetime2(0) NULL
 );
GO

CREATE TABLE [dbo].[Revisoes] (
    [id] int IDENTITY(1,1) NOT NULL,
    [tipo_revisao] varchar(50) NULL,
    [decisao] varchar(100) NULL,
    [comentario] nvarchar(MAX) NULL,
    [data_hora] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [usuario_id] int NOT NULL,
    [pessoa_id] int NOT NULL,
    [empresa_id] int NOT NULL
 );
GO

CREATE TABLE [dbo].[Usuarios] (
    [id] int IDENTITY(1,1) NOT NULL,
    [nome] nvarchar(150) NOT NULL,
    [email] nvarchar(254) NOT NULL,
    [senha_hash] nvarchar(255) NOT NULL,
    [versao_sessao] bigint NOT NULL CONSTRAINT DF_Usuarios_VersaoSessao DEFAULT (0),
    [status] varchar(20) NOT NULL DEFAULT ('ATIVO'),
    [perfil_acesso_id] int NOT NULL,
    [data_criacao] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [data_atualizacao] datetime2(0) NOT NULL DEFAULT (sysutcdatetime())
 );
GO

ALTER TABLE [dbo].[CNAEs] ADD CONSTRAINT [PK_CNAEs] PRIMARY KEY ([numero]);
CREATE INDEX [IX_CNAEsEmpresas_Empresa] ON [dbo].[CNAEs_Empresas] ([empresa_id]);
ALTER TABLE [dbo].[CNAEs_Empresas] ADD CONSTRAINT [PK_CNAEsEmpresas] PRIMARY KEY ([cnae_numero], [empresa_id]);
CREATE UNIQUE INDEX [UX_CNAEsEmpresas_Principal] ON [dbo].[CNAEs_Empresas] ([empresa_id]) WHERE ([principal]=(1));
ALTER TABLE [dbo].[Conjuntos_permissoes] ADD CONSTRAINT [PK_ConjuntosPermissoes] PRIMARY KEY ([id]);
CREATE UNIQUE INDEX [UQ_ConjuntosPermissoes_Nome] ON [dbo].[Conjuntos_permissoes] ([nome]);
CREATE INDEX [IX_DadosColetados_Empresa] ON [dbo].[Dados_coletados] ([empresa_id], [campo]) WHERE ([empresa_id] IS NOT NULL);
CREATE INDEX [IX_DadosColetados_Fonte] ON [dbo].[Dados_coletados] ([fonte_dados_id], [data_coleta] DESC);
CREATE INDEX [IX_DadosColetados_Pessoa] ON [dbo].[Dados_coletados] ([pessoa_id], [campo]) WHERE ([pessoa_id] IS NOT NULL);
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [PK_DadosColetados] PRIMARY KEY ([id]);
CREATE INDEX [IX_Empresas_CidadeEstado] ON [dbo].[Empresas] ([estado], [cidade]);
CREATE INDEX [IX_Empresas_NomeFantasia] ON [dbo].[Empresas] ([nome_fantasia]);
CREATE INDEX [IX_Empresas_Segmento] ON [dbo].[Empresas] ([segmento]);
ALTER TABLE [dbo].[Empresas] ADD CONSTRAINT [PK_Empresas] PRIMARY KEY ([id]);
CREATE UNIQUE INDEX [UX_Empresas_CNPJ] ON [dbo].[Empresas] ([cnpj]) WHERE ([cnpj] IS NOT NULL);
CREATE INDEX [IX_Exportacoes_Usuario] ON [dbo].[Exportacoes] ([usuario_id], [data_hora] DESC);
ALTER TABLE [dbo].[Exportacoes] ADD CONSTRAINT [PK_Exportacoes] PRIMARY KEY ([id]);
ALTER TABLE [dbo].[Exportacoes_Pessoas_Empresas] ADD CONSTRAINT [PK_ExportacoesPessoasEmpresas] PRIMARY KEY ([exportacao_id], [pessoa_id], [empresa_id]);
ALTER TABLE [dbo].[Fontes_dados] ADD CONSTRAINT [PK_FontesDados] PRIMARY KEY ([id]);
CREATE INDEX [IX_Logs_Usuario_Data] ON [dbo].[Logs] ([usuario_id], [data_hora] DESC);
ALTER TABLE [dbo].[Logs] ADD CONSTRAINT [PK_Logs] PRIMARY KEY ([id]);
ALTER TABLE [dbo].[Perfis_acesso] ADD CONSTRAINT [PK_PerfisAcesso] PRIMARY KEY ([id]);
CREATE UNIQUE INDEX [UQ_PerfisAcesso_Nome] ON [dbo].[Perfis_acesso] ([nome]);
CREATE INDEX [IX_Pessoas_Email] ON [dbo].[Pessoas] ([email_profissional]);
CREATE INDEX [IX_Pessoas_Nome] ON [dbo].[Pessoas] ([nome]);
ALTER TABLE [dbo].[Pessoas] ADD CONSTRAINT [PK_Pessoas] PRIMARY KEY ([id]);
CREATE INDEX [IX_PessoasEmpresas_Empresa] ON [dbo].[Pessoas_Empresas] ([empresa_id], [departamento], [senioridade]);
CREATE INDEX [IX_PessoasEmpresas_Lead] ON [dbo].[Pessoas_Empresas] ([status_lead], [nivel_confianca]);
ALTER TABLE [dbo].[Pessoas_Empresas] ADD CONSTRAINT [PK_PessoasEmpresas] PRIMARY KEY ([pessoa_id], [empresa_id]);
CREATE INDEX [IX_Revisoes_Vinculo] ON [dbo].[Revisoes] ([pessoa_id], [empresa_id], [data_hora] DESC);
ALTER TABLE [dbo].[Revisoes] ADD CONSTRAINT [PK_Revisoes] PRIMARY KEY ([id]);
ALTER TABLE [dbo].[Usuarios] ADD CONSTRAINT [PK_Usuarios] PRIMARY KEY ([id]);
CREATE UNIQUE INDEX [UQ_Usuarios_Email] ON [dbo].[Usuarios] ([email]);
ALTER TABLE [dbo].[Fontes_dados] ADD CONSTRAINT [CK_FontesDados_Confiabilidade] CHECK ([confiabilidade] IS NULL OR [confiabilidade]>=(0) AND [confiabilidade]<=(100));
ALTER TABLE [dbo].[Fontes_dados] ADD CONSTRAINT [CK_FontesDados_Limite] CHECK ([limite_requisicoes] IS NULL OR [limite_requisicoes]>=(0));
ALTER TABLE [dbo].[Usuarios] ADD CONSTRAINT [CK_Usuarios_Status] CHECK ([status]='DESATIVADO' OR [status]='BLOQUEADO' OR [status]='ATIVO');
ALTER TABLE [dbo].[Empresas] ADD CONSTRAINT [CK_Empresas_TipoUnidade] CHECK ([tipo_unidade] IS NULL OR ([tipo_unidade]='FILIAL' OR [tipo_unidade]='MATRIZ'));
ALTER TABLE [dbo].[Empresas] ADD CONSTRAINT [CK_Empresas_QtdColaboradores] CHECK ([qtd_colaboradores_aprox] IS NULL OR [qtd_colaboradores_aprox]>=(0));
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [CK_DadosColetados_Destino] CHECK ([pessoa_id] IS NOT NULL AND [empresa_id] IS NULL OR [pessoa_id] IS NULL AND [empresa_id] IS NOT NULL);
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [CK_DadosColetados_Confianca] CHECK ([nivel_confianca] IS NULL OR [nivel_confianca]>=(0) AND [nivel_confianca]<=(100));
ALTER TABLE [dbo].[Pessoas_Empresas] ADD CONSTRAINT [CK_PessoasEmpresas_Confianca] CHECK ([nivel_confianca] IS NULL OR [nivel_confianca]>=(0) AND [nivel_confianca]<=(100));
ALTER TABLE [dbo].[Exportacoes] ADD CONSTRAINT [CK_Exportacoes_Qtd] CHECK ([qntd_itens]>=(0));
ALTER TABLE [dbo].[CNAEs_Empresas] ADD CONSTRAINT [FK_CNAEsEmpresas_CNAEs] FOREIGN KEY ([cnae_numero]) REFERENCES [dbo].[CNAEs] ([numero]);
ALTER TABLE [dbo].[CNAEs_Empresas] ADD CONSTRAINT [FK_CNAEsEmpresas_Empresas] FOREIGN KEY ([empresa_id]) REFERENCES [dbo].[Empresas] ([id]);
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [FK_DadosColetados_Empresas] FOREIGN KEY ([empresa_id]) REFERENCES [dbo].[Empresas] ([id]);
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [FK_DadosColetados_FontesDados] FOREIGN KEY ([fonte_dados_id]) REFERENCES [dbo].[Fontes_dados] ([id]);
ALTER TABLE [dbo].[Dados_coletados] ADD CONSTRAINT [FK_DadosColetados_Pessoas] FOREIGN KEY ([pessoa_id]) REFERENCES [dbo].[Pessoas] ([id]);
ALTER TABLE [dbo].[Exportacoes] ADD CONSTRAINT [FK_Exportacoes_Usuarios] FOREIGN KEY ([usuario_id]) REFERENCES [dbo].[Usuarios] ([id]);
ALTER TABLE [dbo].[Exportacoes_Pessoas_Empresas] ADD CONSTRAINT [FK_ExportacoesPE_Exportacoes] FOREIGN KEY ([exportacao_id]) REFERENCES [dbo].[Exportacoes] ([id]);
ALTER TABLE [dbo].[Exportacoes_Pessoas_Empresas] ADD CONSTRAINT [FK_ExportacoesPE_PessoasEmpresas] FOREIGN KEY ([pessoa_id],[empresa_id]) REFERENCES [dbo].[Pessoas_Empresas] ([pessoa_id],[empresa_id]);
ALTER TABLE [dbo].[Logs] ADD CONSTRAINT [FK_Logs_Usuarios] FOREIGN KEY ([usuario_id]) REFERENCES [dbo].[Usuarios] ([id]);
ALTER TABLE [dbo].[Perfis_acesso] ADD CONSTRAINT [FK_PerfisAcesso_ConjuntosPermissoes] FOREIGN KEY ([conjunto_permissoes_id]) REFERENCES [dbo].[Conjuntos_permissoes] ([id]);
ALTER TABLE [dbo].[Pessoas_Empresas] ADD CONSTRAINT [FK_PessoasEmpresas_Empresas] FOREIGN KEY ([empresa_id]) REFERENCES [dbo].[Empresas] ([id]);
ALTER TABLE [dbo].[Pessoas_Empresas] ADD CONSTRAINT [FK_PessoasEmpresas_Pessoas] FOREIGN KEY ([pessoa_id]) REFERENCES [dbo].[Pessoas] ([id]);
ALTER TABLE [dbo].[Revisoes] ADD CONSTRAINT [FK_Revisoes_PessoasEmpresas] FOREIGN KEY ([pessoa_id],[empresa_id]) REFERENCES [dbo].[Pessoas_Empresas] ([pessoa_id],[empresa_id]);
ALTER TABLE [dbo].[Revisoes] ADD CONSTRAINT [FK_Revisoes_Usuarios] FOREIGN KEY ([usuario_id]) REFERENCES [dbo].[Usuarios] ([id]);
ALTER TABLE [dbo].[Usuarios] ADD CONSTRAINT [FK_Usuarios_PerfisAcesso] FOREIGN KEY ([perfil_acesso_id]) REFERENCES [dbo].[Perfis_acesso] ([id]);
GO

-- Somente os conjuntos e perfis básicos existentes. Nenhum usuário ou credencial.
INSERT INTO dbo.Conjuntos_permissoes (nome,descricao) VALUES
(N'Administracao',N'Administração do sistema'),(N'Operacao Comercial',N'Cadastros e revisão'),(N'Leitura',N'Consulta');
INSERT INTO dbo.Perfis_acesso (nome,conjunto_permissoes_id) VALUES
(N'Administrador',1),(N'Gestor Comercial',2),(N'Analista',3);
GO
