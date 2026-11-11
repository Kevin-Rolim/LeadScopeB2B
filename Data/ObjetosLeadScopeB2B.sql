-- Objetos de acesso a dados do LeadScopeB2B.
-- Execute após instalar as 14 tabelas, com sqlcmd -b ou no SSMS.
USE LeadScopeB2B;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Views das tabelas e consultas compostas.

CREATE OR ALTER VIEW dbo.vw_ConjuntoPermissoes
AS
SELECT Id, Nome, Descricao
FROM dbo.ConjuntoPermissoes;
GO

CREATE OR ALTER VIEW dbo.vw_FonteDados
AS
SELECT Id, Nome, Tipo, Url, Confiabilidade, DataHoraModificacao, DataHoraExclusao
FROM dbo.FonteDados;
GO

CREATE OR ALTER VIEW dbo.vw_PerfisAcesso
AS
SELECT Id, Nome, ConjuntoPermissoesId
FROM dbo.PerfisAcesso;
GO

CREATE OR ALTER VIEW dbo.vw_Usuarios
AS
SELECT Id, Nome, Email, PerfilAcessoId
FROM dbo.Usuarios;
GO

CREATE OR ALTER VIEW dbo.vw_Logs
AS
SELECT Id, TipoEvento, DescricaoEvento, DataHora, DadosAlterados, UsuarioId
FROM dbo.Logs;
GO

CREATE OR ALTER VIEW dbo.vw_Cnaes
AS
SELECT Numero, Descricao
FROM dbo.Cnaes;
GO

CREATE OR ALTER VIEW dbo.vw_Pessoas
AS
SELECT Id, Nome, Email, Telefone, UrlLinkedIn, Status, Origem, DataColeta, DataBloqueio, DataExclusao
FROM dbo.Pessoas;
GO

CREATE OR ALTER VIEW dbo.vw_Empresas
AS
SELECT Id, RazaoSocial, NomeFantasia, Cnpj, UrlSite, Segmento, Porte, Cidade, Estado, Email, Telefone, Status, DataCriacao, DataAtualizacao
FROM dbo.Empresas;
GO

CREATE OR ALTER VIEW dbo.vw_DadosColetados
AS
SELECT Id, Campo, Valor, NivelConfianca, FonteDadosId, PessoaId, EmpresaId
FROM dbo.DadosColetados;
GO

CREATE OR ALTER VIEW dbo.vw_CnaesEmpresas
AS
SELECT CnaeNumero, EmpresaId
FROM dbo.CnaesEmpresas;
GO

CREATE OR ALTER VIEW dbo.vw_PessoasEmpresas
AS
SELECT PessoaId, EmpresaId, Cargo, Departamento, Senioridade, StatusVinculo, StatusLead, DataRevisao, NivelConfianca, FonteVinculo, DataVerificacao
FROM dbo.PessoasEmpresas;
GO

CREATE OR ALTER VIEW dbo.vw_Revisoes
AS
SELECT Id, Decisao, Comentario, DataHora, UsuarioId, PessoaId, EmpresaId
FROM dbo.Revisoes;
GO

CREATE OR ALTER VIEW dbo.vw_Exportacoes
AS
SELECT Id, QtdItens, DataHora, UsuarioId
FROM dbo.Exportacoes;
GO

CREATE OR ALTER VIEW dbo.vw_ExportacoesPessoasEmpresas
AS
SELECT ExportacoesId, PessoaId, EmpresaId
FROM dbo.ExportacoesPessoasEmpresas;
GO

CREATE OR ALTER VIEW dbo.vw_EmpresasCnaes
AS
SELECT e.Id AS EmpresaId, e.RazaoSocial, e.Cnpj, c.Numero AS CnaeNumero, c.Descricao AS Atividade
FROM dbo.CnaesEmpresas AS ce
JOIN dbo.Empresas AS e ON e.Id = ce.EmpresaId
JOIN dbo.Cnaes AS c ON c.Numero = ce.CnaeNumero;
GO

CREATE OR ALTER VIEW dbo.vw_VinculosDetalhados
AS
SELECT pe.PessoaId, p.Nome AS Pessoa, p.Email AS EmailProfissional, p.Telefone AS TelefoneProfissional,
       p.Status AS StatusPessoa, p.DataBloqueio, p.DataExclusao,
       pe.EmpresaId, e.RazaoSocial AS Empresa, e.NomeFantasia, e.Cnpj, e.Segmento, e.Cidade, e.Estado,
       e.Status AS StatusEmpresa, pe.Cargo, pe.Departamento, pe.Senioridade, pe.StatusVinculo,
       pe.StatusLead, pe.DataRevisao, pe.NivelConfianca, pe.FonteVinculo, pe.DataVerificacao
FROM dbo.PessoasEmpresas AS pe
JOIN dbo.Pessoas AS p ON p.Id = pe.PessoaId
JOIN dbo.Empresas AS e ON e.Id = pe.EmpresaId;
GO

CREATE OR ALTER VIEW dbo.vw_LeadsDetalhados
AS
SELECT PessoaId, Pessoa, EmailProfissional, TelefoneProfissional, EmpresaId, Empresa, Cnpj,
       Segmento, Cidade, Estado, Cargo, Departamento, Senioridade, StatusVinculo, StatusLead,
       NivelConfianca, FonteVinculo, DataVerificacao
FROM dbo.vw_VinculosDetalhados
WHERE DataBloqueio IS NULL AND DataExclusao IS NULL;
GO

CREATE OR ALTER VIEW dbo.vw_LeadsCadastro
AS
SELECT PessoaId, Pessoa AS PessoaNome, EmpresaId, Empresa AS RazaoSocial, NomeFantasia,
       Cargo, Departamento, Senioridade, StatusVinculo, StatusLead, DataRevisao,
       NivelConfianca, FonteVinculo, DataVerificacao
FROM dbo.vw_VinculosDetalhados
WHERE DataExclusao IS NULL
  AND (StatusEmpresa IS NULL OR StatusEmpresa <> 'REMOVIDA')
  AND (StatusLead IS NULL OR StatusLead <> 'REMOVIDO');
GO

CREATE OR ALTER VIEW dbo.vw_PerfisConjuntos
AS
SELECT p.Id, p.Nome, p.ConjuntoPermissoesId, c.Nome AS ConjuntoPermissoes,
       c.Descricao AS DescricaoPermissoes
FROM dbo.PerfisAcesso AS p
JOIN dbo.ConjuntoPermissoes AS c ON c.Id = p.ConjuntoPermissoesId;
GO

CREATE OR ALTER VIEW dbo.vw_UsuariosPerfis
AS
SELECT u.Id AS UsuarioId, u.Nome AS Usuario, u.Email, p.Id AS PerfilId, p.Nome AS Perfil,
       p.ConjuntoPermissoesId AS ConjuntoId, p.ConjuntoPermissoes, p.DescricaoPermissoes
FROM dbo.Usuarios AS u
JOIN dbo.vw_PerfisConjuntos AS p ON p.Id = u.PerfilAcessoId;
GO

CREATE OR ALTER VIEW dbo.vw_DadosEmpresasFonte
AS
SELECT d.Id AS DadoId, e.Id AS EmpresaId, e.RazaoSocial, d.Campo, d.Valor,
       d.NivelConfianca, f.Id AS FonteId, f.Nome AS Fonte, f.Tipo AS TipoFonte
FROM dbo.DadosColetados AS d
JOIN dbo.Empresas AS e ON e.Id = d.EmpresaId
JOIN dbo.FonteDados AS f ON f.Id = d.FonteDadosId;
GO

CREATE OR ALTER VIEW dbo.vw_DadosPessoasFonte
AS
SELECT d.Id AS DadoId, p.Id AS PessoaId, p.Nome AS Pessoa, d.Campo, d.Valor,
       d.NivelConfianca, f.Id AS FonteId, f.Nome AS Fonte, f.Tipo AS TipoFonte
FROM dbo.DadosColetados AS d
JOIN dbo.Pessoas AS p ON p.Id = d.PessoaId
JOIN dbo.FonteDados AS f ON f.Id = d.FonteDadosId
WHERE p.DataBloqueio IS NULL AND p.DataExclusao IS NULL;
GO

CREATE OR ALTER VIEW dbo.vw_RevisoesLeads
AS
SELECT r.Id AS RevisaoId, r.DataHora, r.Decisao, r.Comentario, u.Id AS RevisorId,
       u.Nome AS Revisor, p.Nome AS Pessoa, e.RazaoSocial AS Empresa, pe.StatusLead, pe.StatusVinculo,
       r.PessoaId, r.EmpresaId
FROM dbo.Revisoes AS r
JOIN dbo.Usuarios AS u ON u.Id = r.UsuarioId
JOIN dbo.PessoasEmpresas AS pe ON pe.PessoaId = r.PessoaId AND pe.EmpresaId = r.EmpresaId
JOIN dbo.Pessoas AS p ON p.Id = pe.PessoaId
JOIN dbo.Empresas AS e ON e.Id = pe.EmpresaId;
GO

CREATE OR ALTER VIEW dbo.vw_ExportacoesLeads
AS
SELECT ex.Id AS ExportacaoId, ex.DataHora, ex.QtdItens, u.Nome AS Usuario,
       p.Nome AS Pessoa, e.RazaoSocial AS Empresa, pe.Cargo, pe.StatusLead
FROM dbo.ExportacoesPessoasEmpresas AS ep
JOIN dbo.Exportacoes AS ex ON ex.Id = ep.ExportacoesId
JOIN dbo.Usuarios AS u ON u.Id = ex.UsuarioId
JOIN dbo.PessoasEmpresas AS pe ON pe.PessoaId = ep.PessoaId AND pe.EmpresaId = ep.EmpresaId
JOIN dbo.Pessoas AS p ON p.Id = pe.PessoaId
JOIN dbo.Empresas AS e ON e.Id = pe.EmpresaId;
GO

CREATE OR ALTER VIEW dbo.vw_LogsUsuarios
AS
SELECT l.Id, l.TipoEvento, l.DescricaoEvento, l.DataHora, l.DadosAlterados, l.UsuarioId,
       u.Nome AS UsuarioNome
FROM dbo.Logs AS l
JOIN dbo.Usuarios AS u ON u.Id = l.UsuarioId;
GO

-- Funções tabulares inline para consultas parametrizadas.

CREATE OR ALTER FUNCTION dbo.fn_LeadExportavel(@PessoaId int, @EmpresaId int)
RETURNS TABLE
AS
RETURN
(
    SELECT pe.PessoaId, pe.EmpresaId, p.Nome, e.RazaoSocial, p.Email, p.Telefone, pe.Cargo
    FROM dbo.vw_PessoasEmpresas AS pe WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.vw_Pessoas AS p ON p.Id = pe.PessoaId
    JOIN dbo.vw_Empresas AS e ON e.Id = pe.EmpresaId
    WHERE pe.PessoaId = @PessoaId AND pe.EmpresaId = @EmpresaId
      AND pe.StatusLead = 'QUALIFICADO' AND pe.StatusVinculo = 'ATIVO'
      AND p.DataExclusao IS NULL AND p.DataBloqueio IS NULL
      AND p.Status = 'VALIDADO' AND e.Status = 'VALIDADA'
);
GO

CREATE OR ALTER FUNCTION dbo.fn_CnaesDaEmpresa(@EmpresaId int)
RETURNS TABLE
AS
RETURN
(
    SELECT c.Numero, c.Descricao
    FROM dbo.vw_CnaesEmpresas AS ce
    JOIN dbo.vw_Cnaes AS c ON c.Numero = ce.CnaeNumero
    WHERE ce.EmpresaId = @EmpresaId
);
GO

-- Procedures de inclusão e alteração das 14 tabelas.

CREATE OR ALTER PROCEDURE dbo.usp_ConjuntoPermissoesAdd
    @Nome varchar(100),
    @Descricao varchar(255) = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ConjuntoPermissoes
        (Nome, Descricao)
    VALUES
        (@Nome, @Descricao);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ConjuntoPermissoesAlt
    @Id int,
    @Nome varchar(100),
    @Descricao varchar(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ConjuntoPermissoes
    SET Nome = @Nome,
        Descricao = @Descricao
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de ConjuntoPermissoes nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FonteDadosAdd
    @Nome varchar(100),
    @Tipo varchar(50) = NULL,
    @Url varchar(255) = NULL,
    @Confiabilidade decimal(5,2) = NULL,
    @DataHoraExclusao datetime2 = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.FonteDados
        (Nome, Tipo, Url, Confiabilidade, DataHoraExclusao)
    VALUES
        (@Nome, @Tipo, @Url, @Confiabilidade, @DataHoraExclusao);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FonteDadosAlt
    @Id int,
    @Nome varchar(100),
    @Tipo varchar(50) = NULL,
    @Url varchar(255) = NULL,
    @Confiabilidade decimal(5,2) = NULL,
    @DataHoraExclusao datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.FonteDados
    SET Nome = @Nome,
        Tipo = @Tipo,
        Url = @Url,
        Confiabilidade = @Confiabilidade,
        DataHoraExclusao = @DataHoraExclusao
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de FonteDados nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PerfisAcessoAdd
    @Nome varchar(100),
    @ConjuntoPermissoesId int,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PerfisAcesso
        (Nome, ConjuntoPermissoesId)
    VALUES
        (@Nome, @ConjuntoPermissoesId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PerfisAcessoAlt
    @Id int,
    @Nome varchar(100),
    @ConjuntoPermissoesId int
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PerfisAcesso
    SET Nome = @Nome,
        ConjuntoPermissoesId = @ConjuntoPermissoesId
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de PerfisAcesso nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosAdd
    @Nome varchar(100),
    @Email varchar(150),
    @SenhaHash varchar(255),
    @PerfilAcessoId int,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Usuarios
        (Nome, Email, SenhaHash, PerfilAcessoId)
    VALUES
        (@Nome, @Email, @SenhaHash, @PerfilAcessoId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosAlt
    @Id int,
    @Nome varchar(100),
    @Email varchar(150),
    @SenhaHash varchar(255),
    @PerfilAcessoId int
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios
    SET Nome = @Nome,
        Email = @Email,
        SenhaHash = @SenhaHash,
        PerfilAcessoId = @PerfilAcessoId
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de Usuarios nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LogsAdd
    @TipoEvento varchar(50),
    @UsuarioId int,
    @DescricaoEvento varchar(MAX) = NULL,
    @DadosAlterados varchar(MAX) = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Logs
        (TipoEvento, DescricaoEvento, DadosAlterados, UsuarioId)
    VALUES
        (@TipoEvento, @DescricaoEvento, @DadosAlterados, @UsuarioId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LogsAlt
    @Id int,
    @TipoEvento varchar(50),
    @DescricaoEvento varchar(MAX) = NULL,
    @DadosAlterados varchar(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Logs
    SET TipoEvento = @TipoEvento,
        DescricaoEvento = @DescricaoEvento,
        DadosAlterados = @DadosAlterados
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de Logs nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesAdd
    @Numero varchar(20),
    @Descricao varchar(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Cnaes
        (Numero, Descricao)
    VALUES
        (@Numero, @Descricao);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesAlt
    @Numero varchar(20),
    @Descricao varchar(255)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Cnaes
    SET Descricao = @Descricao
    WHERE Numero = @Numero;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de Cnaes nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasAdd
    @Nome varchar(150),
    @Email varchar(150) = NULL,
    @Telefone varchar(20) = NULL,
    @UrlLinkedIn varchar(255) = NULL,
    @Status varchar(50) = NULL,
    @Origem varchar(100) = NULL,
    @DataBloqueio datetime2 = NULL,
    @DataExclusao datetime2 = NULL,
    @NovoId int = NULL OUTPUT,
    @DataColeta datetime2 = NULL,
    @InformarDataColeta bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Pessoas
        (Nome, Email, Telefone, UrlLinkedIn, Status, Origem, DataBloqueio, DataExclusao, DataColeta)
    VALUES
        (@Nome, @Email, @Telefone, @UrlLinkedIn, @Status, @Origem, @DataBloqueio, @DataExclusao,
         CASE WHEN @InformarDataColeta = 1 THEN @DataColeta ELSE SYSDATETIME() END);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasAlt
    @Id int,
    @Nome varchar(150),
    @Email varchar(150) = NULL,
    @Telefone varchar(20) = NULL,
    @UrlLinkedIn varchar(255) = NULL,
    @Status varchar(50) = NULL,
    @Origem varchar(100) = NULL,
    @DataBloqueio datetime2 = NULL,
    @DataExclusao datetime2 = NULL,
    @SomenteAtivo bit = 0,
    @Linhas int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Pessoas
    SET Nome = @Nome,
        Email = @Email,
        Telefone = @Telefone,
        UrlLinkedIn = @UrlLinkedIn,
        Status = @Status,
        Origem = @Origem,
        DataBloqueio = @DataBloqueio,
        DataExclusao = @DataExclusao
    WHERE Id = @Id
      AND (@SomenteAtivo = 0 OR (DataExclusao IS NULL));

    SET @Linhas = @@ROWCOUNT;
    IF @Linhas = 0 AND @SomenteAtivo = 0
        THROW 51001, 'Registro de Pessoas nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_EmpresasAdd
    @RazaoSocial varchar(200),
    @NomeFantasia varchar(200) = NULL,
    @Cnpj varchar(18) = NULL,
    @UrlSite varchar(255) = NULL,
    @Segmento varchar(100) = NULL,
    @Porte varchar(50) = NULL,
    @Cidade varchar(100) = NULL,
    @Estado char(2) = NULL,
    @Email varchar(150) = NULL,
    @Telefone varchar(20) = NULL,
    @Status varchar(50) = NULL,
    @NovoId int = NULL OUTPUT,
    @DataCriacao datetime2 = NULL,
    @DataAtualizacao datetime2 = NULL,
    @InformarDatas bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Empresas
        (RazaoSocial, NomeFantasia, Cnpj, UrlSite, Segmento, Porte, Cidade, Estado, Email, Telefone, Status, DataCriacao, DataAtualizacao)
    VALUES
        (@RazaoSocial, @NomeFantasia, @Cnpj, @UrlSite, @Segmento, @Porte, @Cidade, @Estado, @Email, @Telefone, @Status,
         CASE WHEN @InformarDatas = 1 THEN @DataCriacao ELSE SYSDATETIME() END,
         CASE WHEN @InformarDatas = 1 THEN @DataAtualizacao ELSE SYSDATETIME() END);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_EmpresasAlt
    @Id int,
    @RazaoSocial varchar(200),
    @NomeFantasia varchar(200) = NULL,
    @Cnpj varchar(18) = NULL,
    @UrlSite varchar(255) = NULL,
    @Segmento varchar(100) = NULL,
    @Porte varchar(50) = NULL,
    @Cidade varchar(100) = NULL,
    @Estado char(2) = NULL,
    @Email varchar(150) = NULL,
    @Telefone varchar(20) = NULL,
    @Status varchar(50) = NULL,
    @DataAtualizacao datetime2 = NULL,
    @SomenteAtivo bit = 0,
    @Linhas int = NULL OUTPUT,
    @InformarDataAtualizacao bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Empresas
    SET RazaoSocial = @RazaoSocial,
        NomeFantasia = @NomeFantasia,
        Cnpj = @Cnpj,
        UrlSite = @UrlSite,
        Segmento = @Segmento,
        Porte = @Porte,
        Cidade = @Cidade,
        Estado = @Estado,
        Email = @Email,
        Telefone = @Telefone,
        Status = @Status,
        DataAtualizacao = CASE WHEN @InformarDataAtualizacao = 1 THEN @DataAtualizacao ELSE DataAtualizacao END
    WHERE Id = @Id
      AND (@SomenteAtivo = 0 OR (Status IS NULL OR Status <> 'REMOVIDA'));

    SET @Linhas = @@ROWCOUNT;
    IF @Linhas = 0 AND @SomenteAtivo = 0
        THROW 51001, 'Registro de Empresas nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_DadosColetadosAdd
    @Campo varchar(100),
    @Valor varchar(MAX),
    @FonteDadosId int,
    @NivelConfianca decimal(5,2) = NULL,
    @PessoaId int = NULL,
    @EmpresaId int = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.DadosColetados
        (Campo, Valor, NivelConfianca, FonteDadosId, PessoaId, EmpresaId)
    VALUES
        (@Campo, @Valor, @NivelConfianca, @FonteDadosId, @PessoaId, @EmpresaId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_DadosColetadosAlt
    @Id int,
    @Campo varchar(100),
    @Valor varchar(MAX),
    @NivelConfianca decimal(5,2) = NULL,
    @FonteDadosId int,
    @PessoaId int = NULL,
    @EmpresaId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.DadosColetados
    SET Campo = @Campo,
        Valor = @Valor,
        NivelConfianca = @NivelConfianca,
        FonteDadosId = @FonteDadosId,
        PessoaId = @PessoaId,
        EmpresaId = @EmpresaId
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de DadosColetados nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesEmpresasAdd
    @CnaeNumero varchar(20),
    @EmpresaId int
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CnaesEmpresas
        (CnaeNumero, EmpresaId)
    VALUES
        (@CnaeNumero, @EmpresaId);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesEmpresasAlt
    @CnaeNumero varchar(20),
    @EmpresaId int,
    @NovoCnaeNumero varchar(20),
    @NovoEmpresaId int
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.CnaesEmpresas
    SET CnaeNumero = @NovoCnaeNumero,
        EmpresaId = @NovoEmpresaId
    WHERE CnaeNumero = @CnaeNumero AND EmpresaId = @EmpresaId;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de CnaesEmpresas nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasEmpresasAdd
    @PessoaId int,
    @EmpresaId int,
    @Cargo varchar(100) = NULL,
    @Departamento varchar(100) = NULL,
    @Senioridade varchar(50) = NULL,
    @StatusVinculo varchar(50) = NULL,
    @StatusLead varchar(50) = NULL,
    @DataRevisao datetime2 = NULL,
    @NivelConfianca decimal(5,2) = NULL,
    @FonteVinculo varchar(100) = NULL,
    @DataVerificacao datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PessoasEmpresas
        (PessoaId, EmpresaId, Cargo, Departamento, Senioridade, StatusVinculo, StatusLead, DataRevisao, NivelConfianca, FonteVinculo, DataVerificacao)
    VALUES
        (@PessoaId, @EmpresaId, @Cargo, @Departamento, @Senioridade, @StatusVinculo, @StatusLead, @DataRevisao, @NivelConfianca, @FonteVinculo, @DataVerificacao);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasEmpresasAlt
    @PessoaId int,
    @EmpresaId int,
    @Cargo varchar(100) = NULL,
    @Departamento varchar(100) = NULL,
    @Senioridade varchar(50) = NULL,
    @StatusVinculo varchar(50) = NULL,
    @StatusLead varchar(50) = NULL,
    @DataRevisao datetime2 = NULL,
    @NivelConfianca decimal(5,2) = NULL,
    @FonteVinculo varchar(100) = NULL,
    @DataVerificacao datetime2 = NULL,
    @SomenteAtivo bit = 0,
    @Linhas int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PessoasEmpresas
    SET Cargo = @Cargo,
        Departamento = @Departamento,
        Senioridade = @Senioridade,
        StatusVinculo = @StatusVinculo,
        StatusLead = @StatusLead,
        DataRevisao = @DataRevisao,
        NivelConfianca = @NivelConfianca,
        FonteVinculo = @FonteVinculo,
        DataVerificacao = @DataVerificacao
    WHERE PessoaId = @PessoaId AND EmpresaId = @EmpresaId
      AND (@SomenteAtivo = 0 OR (StatusLead IS NULL OR StatusLead <> 'REMOVIDO'));

    SET @Linhas = @@ROWCOUNT;
    IF @Linhas = 0 AND @SomenteAtivo = 0
        THROW 51001, 'Registro de PessoasEmpresas nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RevisoesAdd
    @UsuarioId int,
    @PessoaId int,
    @EmpresaId int,
    @Decisao varchar(100) = NULL,
    @Comentario varchar(MAX) = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Revisoes
        (Decisao, Comentario, UsuarioId, PessoaId, EmpresaId)
    VALUES
        (@Decisao, @Comentario, @UsuarioId, @PessoaId, @EmpresaId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RevisoesAlt
    @Id int,
    @Decisao varchar(100) = NULL,
    @Comentario varchar(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Revisoes
    SET Decisao = @Decisao,
        Comentario = @Comentario
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de Revisoes nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ExportacoesAdd
    @UsuarioId int,
    @QtdItens int = NULL,
    @NovoId int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Exportacoes
        (QtdItens, UsuarioId)
    VALUES
        (@QtdItens, @UsuarioId);
    SET @NovoId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ExportacoesAlt
    @Id int,
    @QtdItens int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Exportacoes
    SET QtdItens = @QtdItens
    WHERE Id = @Id;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de Exportacoes nao encontrado para atualizacao.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ExportacoesPessoasEmpresasAdd
    @ExportacoesId int,
    @PessoaId int,
    @EmpresaId int
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ExportacoesPessoasEmpresas
        (ExportacoesId, PessoaId, EmpresaId)
    VALUES
        (@ExportacoesId, @PessoaId, @EmpresaId);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ExportacoesPessoasEmpresasAlt
    @ExportacoesId int,
    @PessoaId int,
    @EmpresaId int,
    @NovoExportacoesId int,
    @NovoPessoaId int,
    @NovoEmpresaId int
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ExportacoesPessoasEmpresas
    SET ExportacoesId = @NovoExportacoesId,
        PessoaId = @NovoPessoaId,
        EmpresaId = @NovoEmpresaId
    WHERE ExportacoesId = @ExportacoesId AND PessoaId = @PessoaId AND EmpresaId = @EmpresaId;

    IF @@ROWCOUNT = 0
        THROW 51001, 'Registro de ExportacoesPessoasEmpresas nao encontrado para atualizacao.', 1;
END;
GO

-- Operações específicas da aplicação.

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosObterPorId
    @Id int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.UsuarioId AS Id, p.Usuario AS Nome, p.Email, u.SenhaHash, p.PerfilId AS PerfilAcessoId,
           p.Perfil AS PerfilNome, p.ConjuntoId AS ConjuntoPermissoesId, p.ConjuntoPermissoes AS ConjuntoNome
    FROM dbo.vw_UsuariosPerfis AS p
    JOIN dbo.Usuarios AS u ON u.Id = p.UsuarioId
    WHERE p.UsuarioId = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosObterPorEmail
    @Email varchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.UsuarioId AS Id, p.Usuario AS Nome, p.Email, u.SenhaHash, p.PerfilId AS PerfilAcessoId,
           p.Perfil AS PerfilNome, p.ConjuntoId AS ConjuntoPermissoesId, p.ConjuntoPermissoes AS ConjuntoNome
    FROM dbo.vw_UsuariosPerfis AS p
    JOIN dbo.Usuarios AS u ON u.Id = p.UsuarioId
    WHERE LOWER(LTRIM(RTRIM(p.Email))) = @Email;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosAlterarPerfil
    @Id int,
    @Nome varchar(100),
    @Email varchar(150),
    @SenhaEsperada varchar(255),
    @Linhas int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios
    SET Nome = @Nome, Email = @Email
    WHERE Id = @Id AND SenhaHash COLLATE Latin1_General_100_BIN2 = @SenhaEsperada;
    SET @Linhas = @@ROWCOUNT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_UsuariosAlterarSenha
    @Id int,
    @Email varchar(150),
    @SenhaEsperada varchar(255),
    @NovaSenha varchar(255),
    @Linhas int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios
    SET SenhaHash = @NovaSenha
    WHERE Id = @Id AND Email = @Email AND SenhaHash COLLATE Latin1_General_100_BIN2 = @SenhaEsperada;
    SET @Linhas = @@ROWCOUNT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_EmpresasExcluir
    @Id int,
    @Linhas int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Empresas
    SET Status = 'REMOVIDA', DataAtualizacao = SYSUTCDATETIME()
    WHERE Id = @Id AND (Status IS NULL OR Status <> 'REMOVIDA');
    SET @Linhas = @@ROWCOUNT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasEmpresasExcluir
    @PessoaId int,
    @EmpresaId int,
    @Linhas int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PessoasEmpresas
    SET StatusLead = 'REMOVIDO', StatusVinculo = 'INATIVO', DataVerificacao = SYSUTCDATETIME()
    WHERE PessoaId = @PessoaId AND EmpresaId = @EmpresaId AND (StatusLead IS NULL OR StatusLead <> 'REMOVIDO');
    SET @Linhas = @@ROWCOUNT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_PessoasEmpresasRevisar
    @PessoaId int,
    @EmpresaId int,
    @Decisao varchar(50),
    @Linhas int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PessoasEmpresas
    SET StatusLead = @Decisao,
        StatusVinculo = CASE @Decisao WHEN 'QUALIFICADO' THEN 'ATIVO' WHEN 'REJEITADO' THEN 'INATIVO' ELSE 'INCERTO' END,
        DataRevisao = SYSUTCDATETIME(), DataVerificacao = SYSUTCDATETIME()
    WHERE PessoaId = @PessoaId AND EmpresaId = @EmpresaId AND (StatusLead IS NULL OR StatusLead <> 'REMOVIDO');
    SET @Linhas = @@ROWCOUNT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesEmpresasAssociar
    @EmpresaId int,
    @CnaeNumero varchar(20)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CnaesEmpresas (CnaeNumero, EmpresaId)
    SELECT @CnaeNumero, @EmpresaId
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CnaesEmpresas WITH (UPDLOCK, HOLDLOCK)
        WHERE CnaeNumero = @CnaeNumero AND EmpresaId = @EmpresaId
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CnaesEmpresasRemover
    @EmpresaId int,
    @CnaeNumero varchar(20)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.CnaesEmpresas WHERE EmpresaId = @EmpresaId AND CnaeNumero = @CnaeNumero;
END;
GO

-- Atualiza a data das fontes modificadas em uma única operação por lote.

CREATE OR ALTER TRIGGER dbo.tr_FonteDados_DataHoraModificacao
ON dbo.FonteDados
AFTER UPDATE
AS
BEGIN
    IF ROWCOUNT_BIG() = 0 RETURN;
    SET NOCOUNT ON;

    IF UPDATE(DataHoraModificacao) RETURN;

    DECLARE @Agora datetime2 = SYSDATETIME();
    UPDATE f
    SET DataHoraModificacao = @Agora
    FROM dbo.FonteDados AS f
    JOIN inserted AS i ON i.Id = f.Id
    JOIN deleted AS d ON d.Id = i.Id
    WHERE EXISTS (
        SELECT CONVERT(varbinary(100), i.Nome), CONVERT(varbinary(50), i.Tipo),
               CONVERT(varbinary(255), i.Url), i.Confiabilidade, i.DataHoraExclusao
        EXCEPT
        SELECT CONVERT(varbinary(100), d.Nome), CONVERT(varbinary(50), d.Tipo),
               CONVERT(varbinary(255), d.Url), d.Confiabilidade, d.DataHoraExclusao
    );
END;
GO

-- Índices para o acesso por empresa, histórico por vínculo e auditoria recente.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CnaesEmpresas') AND name = N'IX_CnaesEmpresas_Empresa')
    CREATE INDEX IX_CnaesEmpresas_Empresa ON dbo.CnaesEmpresas (EmpresaId, CnaeNumero);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PessoasEmpresas') AND name = N'IX_PessoasEmpresas_Empresa')
    CREATE INDEX IX_PessoasEmpresas_Empresa ON dbo.PessoasEmpresas (EmpresaId, PessoaId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Revisoes') AND name = N'IX_Revisoes_Vinculo_Data')
    CREATE INDEX IX_Revisoes_Vinculo_Data ON dbo.Revisoes (PessoaId, EmpresaId, DataHora DESC, Id DESC)
    INCLUDE (UsuarioId, Decisao);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Logs') AND name = N'IX_Logs_Data')
    CREATE INDEX IX_Logs_Data ON dbo.Logs (DataHora DESC, Id DESC) INCLUDE (UsuarioId, TipoEvento);
GO
