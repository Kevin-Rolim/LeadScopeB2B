USE LeadScopeB2B;
GO

-- =========================================================
-- 1. DADOS BASE
-- =========================================================

INSERT INTO ConjuntoPermissoes (Nome, Descricao)
VALUES
('Admin', 'Acesso total ao sistema'),
('Gestor', 'Acesso a dashboards, filtros e revisão de leads'),
('Leitura', 'Acesso somente leitura');
GO

INSERT INTO FonteDados (Nome, Tipo, Url, Confiabilidade)
VALUES
('LinkedIn API', 'API', 'https://linkedin.com', 95.50),
('Scraper Site Corporativo', 'Web Scraping', 'https://exemplo.com',
88.00),
('Base Interna CRM', 'Banco Interno', NULL, 99.00);
GO

INSERT INTO PerfisAcesso (Nome, ConjuntoPermissoesId)
VALUES
('Administrador Geral', 1),
('Gestor Comercial', 2),
('Analista Comercial', 3);
GO

INSERT INTO Usuarios (Nome, Email, Senha, PerfilAcessoId)
VALUES
('Guilherme Santos', 'guilherme@leadscope.com',
'senhaCriptografada123', 1),
('Roberto Almeida', 'roberto@leadscope.com', 'senhaCriptografada456',
2),
('Ana Souza', 'ana@leadscope.com', 'senhaCriptografada789', 3);
GO

INSERT INTO Logs (TiporEvento, DescricaoEvento, DadosAlterados, UsuarioId)
VALUES
('Criação de Conta', 'Usuário administrador inicial criado.', NULL, 1),
('Login', 'Usuário acessou o sistema com sucesso.', NULL, 2),
('Revisão de Lead', 'Lead aprovado após validação manual.',
'status_lead=aprovado', 3);
GO

-- =========================================================
-- 2. ENTIDADES PRINCIPAIS
-- =========================================================

INSERT INTO Cnaes (Numero, Descricao)
VALUES
('6201-5/01', 'Desenvolvimento de programas de computador sob
encomenda'),
('6311-9/00', 'Tratamento de dados, provedores de serviços de aplicação
e hospedagem na internet'),
('7020-4/00', 'Atividades de consultoria em gestão empresarial, exceto
consultoria técnica específica');
GO

INSERT INTO Pessoas (Nome, Email, Telefone, UrlLinkedin,
Status, Origem)
VALUES
('Carlos Eduardo', 'carlos.eduardo@email.com', '(17) 99999-1234',
'linkedin.com/in/carloseduardo', 'Qualificado', 'LinkedIn'),
('Marina Lima', 'marina.lima@email.com', '(17) 98888-5678',
'linkedin.com/in/marinalima', 'Novo', 'Site Corporativo'),
('Rafael Costa', 'rafael.costa@email.com', '(11) 97777-4321',
'linkedin.com/in/rafaelcosta', 'Em análise', 'Base Interna CRM');
GO

INSERT INTO Empresas (RazaoSocial, NomeFantasia, Cnpj, UrlSite,
Segmento, Porte, Cidade, Estado, Email, Telefone, Status)
VALUES
('Tech Soluções Inovadoras LTDA', 'Tech Soluções',
'12.345.678/0001-99', 'www.techsolucoes.com.br', 'Tecnologia da
Informação', 'Média', 'São José do Rio Preto', 'SP',
'contato@techsolucoes.com.br', '(17) 3333-1111', 'Ativa'),
('Cloud B2B Serviços Digitais LTDA', 'Cloud B2B', '98.765.432/0001-11',
'www.cloudb2b.com.br', 'SaaS', 'Pequena', 'Ribeirão Preto', 'SP',
'contato@cloudb2b.com.br', '(16) 3222-2222', 'Ativa'),
('Alpha Consultoria Empresarial LTDA', 'Alpha Consultoria',
'11.222.333/0001-44', 'www.alphaconsultoria.com.br', 'Consultoria',
'Pequena', 'Belo Horizonte', 'MG', 'contato@alphaconsultoria.com.br',
'(31) 4000-9000', 'Ativa');
GO

-- =========================================================
-- 3. DADOS COLETADOS
-- =========================================================

INSERT INTO DadosColetados (Campo, Valor, NivelConfianca,
FonteDadosID, PessoaId, EmpresaId)
VALUES

('email', 'carlos.eduardo@email.com', 98.00, 1, 1, NULL),
('cargo', 'Gerente de TI', 92.00, 1, 1, NULL),
('telefone', '(17) 98888-5678', 95.00, 2, 2, NULL),
('linkedin', 'linkedin.com/in/rafaelcosta', 96.00, 3, 3, NULL),
('site', 'www.techsolucoes.com.br', 99.00, 2, NULL, 1),
('segmento', 'Tecnologia da Informação', 90.00, 3, NULL, 1),
('email', 'contato@cloudb2b.com.br', 97.00, 3, NULL, 2),
('cnpj', '11.222.333/0001-44', 99.00, 1, NULL, 3),
('telefone', '(31) 4000-9000', 94.00, 2, NULL, 3);
GO

-- =========================================================
-- 4. RELACIONAMENTOS N:N
-- =========================================================

INSERT INTO CnaesEmpresas (CnaeNumero, EmpresaId)
VALUES
('6201-5/01', 1),
('6311-9/00', 2),
('7020-4/00', 3);
GO

INSERT INTO PessoasEmpresas
(
PessoaId,
EmpresaId,
Cargo,
Departamento,
Senioridade,
StatusVinculo,
StatusLead,
DataRevisao,
NivelConfianca,
FonteVinculo,
DataVerif
)
VALUES
(1, 1, 'Gerente de TI', 'Tecnologia', 'Sênior', 'Ativo', 'Aprovado',
SYSDATETIME(), 95.00, 'LinkedIn', SYSDATETIME()),
(2, 2, 'Analista Comercial', 'Comercial', 'Pleno', 'Ativo', 'Aprovado',
SYSDATETIME(), 90.00, 'Site Corporativo', SYSDATETIME()),
(3, 3, 'Diretor de Operações', 'Operações', 'Sênior', 'Ativo',
'Aprovado', SYSDATETIME(), 93.00, 'Base Interna CRM', SYSDATETIME());
GO

INSERT INTO Revisoes (Decisao, Comentario, UsuarioId, PessoaId,
EmpresaId)
VALUES
('Aprovado', 'Vínculo validado manualmente e com boa confiabilidade.',
1, 1, 1),
('Aprovado', 'Dados consistentes com a base e sem duplicidade
aparente.', 2, 2, 2),
('Aprovado', 'Lead confirmado após revisão das informações coletadas.',
3, 3, 3);
GO

INSERT INTO Exportacoes (QtdItens, UsuarioId)
VALUES
(1, 1),
(2, 2),
(3, 3);
GO

INSERT INTO ExportacoesPessoasEmpresas (ExportacoesId, PessoaId,
EmpresaId)
VALUES
(1, 1, 1),
(2, 1, 1),
(2, 2, 2),
(3, 1, 1),
(3, 2, 2),
(3, 3, 3);
GO
