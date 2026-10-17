using LeadScopeB2B.Repositories.Interfaces;

namespace LeadScopeB2B.Services;

public class LeadService
{
    private readonly ILeadRepository _repository;

    public LeadService(ILeadRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LeadListViewModel>> ListarAsync(string? busca = null, string? status = null, int? pessoaId = null, int? empresaId = null, string? cnae = null)
    {
        var leads = await _repository.ListarAsync(CadastroRules.Limpar(busca), CadastroRules.Limpar(status), pessoaId, empresaId, CadastroRules.Limpar(cnae) is string numero ? CnaeService.Normalizar(numero) : null);
        return leads.Select(l => new LeadListViewModel
        {
            PessoaId = l.PessoaId,
            EmpresaId = l.EmpresaId,
            Pessoa = l.Pessoas.Nome,
            Empresa = NomeEmpresa(l.Empresas),
            Cargo = l.Cargo,
            Senioridade = l.Senioridade,
            StatusLead = l.StatusLead,
            NivelConfianca = l.NivelConfianca
        }).ToList();
    }

    public async Task<LeadDetailsViewModel?> ObterDetalhesAsync(int pessoaId, int empresaId)
    {
        var lead = await _repository.ObterAsync(pessoaId, empresaId);
        if (lead is null)
        {
            return null;
        }

        return new LeadDetailsViewModel
        {
            PessoaId = lead.PessoaId,
            EmpresaId = lead.EmpresaId,
            Pessoa = lead.Pessoas.Nome,
            Empresa = NomeEmpresa(lead.Empresas),
            Cargo = lead.Cargo,
            Departamento = lead.Departamento,
            Senioridade = lead.Senioridade,
            StatusVinculo = lead.StatusVinculo,
            StatusLead = lead.StatusLead,
            DataRevisao = lead.DataRevisao,
            NivelConfianca = lead.NivelConfianca,
            FonteVinculo = lead.FonteVinculo,
            DataVerificacao = lead.DataVerif,
            Revisoes = await _repository.ListarRevisoesAsync(pessoaId, empresaId)
        };
    }

    public async Task PrepararFormularioAsync(LeadFormViewModel model)
    {
        model.PessoasDisponiveis = await _repository.ListarPessoasAsync();
        model.EmpresasDisponiveis = await _repository.ListarEmpresasAsync();
    }

    public async Task<LeadFormViewModel?> ObterParaEdicaoAsync(int pessoaId, int empresaId)
    {
        var lead = await _repository.ObterAsync(pessoaId, empresaId);
        if (lead is null)
        {
            return null;
        }

        var model = new LeadFormViewModel
        {
            PessoaId = lead.PessoaId,
            EmpresaId = lead.EmpresaId,
            Cargo = lead.Cargo,
            Departamento = lead.Departamento,
            Senioridade = lead.Senioridade,
            StatusVinculo = lead.StatusVinculo,
            StatusLead = lead.StatusLead is "NOVO" or "EM_ANALISE" ? lead.StatusLead : "EM_ANALISE",
            NivelConfianca = lead.NivelConfianca,
            FonteVinculo = lead.FonteVinculo
        };
        await PrepararFormularioAsync(model);
        return model;
    }

    public async Task CadastrarAsync(LeadFormViewModel model)
    {
        CadastroRules.Validar(model);
        if (!await _repository.PessoaExisteAsync(model.PessoaId) ||
            !await _repository.EmpresaExisteAsync(model.EmpresaId))
        {
            throw new ConflitoException("A pessoa ou a empresa selecionada não existe.");
        }

        if (await _repository.ObterAsync(model.PessoaId, model.EmpresaId) is not null)
        {
            throw new ConflitoException("Esse vínculo entre pessoa e empresa já está cadastrado.");
        }

        var lead = new PessoasEmpresas
        {
            PessoaId = model.PessoaId,
            EmpresaId = model.EmpresaId,
            DataVerif = DateTime.UtcNow
        };
        Aplicar(model, lead);
        await _repository.AdicionarAsync(lead);
    }

    public async Task EditarAsync(int pessoaId, int empresaId, LeadFormViewModel model)
    {
        var lead = await _repository.ObterAsync(pessoaId, empresaId)
            ?? throw new KeyNotFoundException("Lead não encontrado.");

        CadastroRules.Validar(model);
        Aplicar(model, lead);
        lead.DataVerif = DateTime.UtcNow;
        await _repository.AtualizarAsync(lead);
    }

    private static void Aplicar(LeadFormViewModel model, PessoasEmpresas lead)
    {
        lead.Cargo = Limpar(model.Cargo);
        lead.Departamento = Limpar(model.Departamento);
        lead.Senioridade = Limpar(model.Senioridade);
        lead.StatusVinculo = CadastroRules.Estado(model.StatusVinculo ?? "INCERTO", "INCERTO", "ATIVO", "INATIVO");
        lead.StatusLead = CadastroRules.Estado(model.StatusLead ?? "NOVO", "NOVO", "EM_ANALISE");
        lead.NivelConfianca = model.NivelConfianca;
        lead.FonteVinculo = Limpar(model.FonteVinculo);
    }

    public Task ExcluirAsync(int pessoaId, int empresaId) => _repository.ExcluirAsync(pessoaId, empresaId);

    public Task RevisarAsync(int pessoaId, int empresaId, int usuarioId, string decisao, string? comentario)
    {
        decisao = CadastroRules.Estado(decisao, "QUALIFICADO", "REJEITADO", "EM_ANALISE");
        if (comentario?.Length > 2000) throw new System.ComponentModel.DataAnnotations.ValidationException("Comentário deve ter até 2000 caracteres.");
        return _repository.RevisarAsync(pessoaId, empresaId, usuarioId, decisao, CadastroRules.Limpar(comentario));
    }

    private static string NomeEmpresa(Empresas empresa)
    {
        return string.IsNullOrWhiteSpace(empresa.NomeFantasia)
            ? empresa.RazaoSocial
            : empresa.NomeFantasia;
    }

    private static string? Limpar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
