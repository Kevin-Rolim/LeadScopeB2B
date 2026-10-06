using LeadScopeB2B.Repositories.Interfaces;

namespace LeadScopeB2B.Services;

public class LeadService
{
    private readonly ILeadRepository _repository;

    public LeadService(ILeadRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LeadListViewModel>> ListarAsync()
    {
        var leads = await _repository.ListarAsync();
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
            DataVerificacao = lead.DataVerif
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
            StatusLead = lead.StatusLead,
            NivelConfianca = lead.NivelConfianca,
            FonteVinculo = lead.FonteVinculo
        };
        await PrepararFormularioAsync(model);
        return model;
    }

    public async Task CadastrarAsync(LeadFormViewModel model)
    {
        if (!await _repository.PessoaExisteAsync(model.PessoaId) ||
            !await _repository.EmpresaExisteAsync(model.EmpresaId))
        {
            throw new InvalidOperationException("A pessoa ou a empresa selecionada não existe.");
        }

        if (await _repository.ObterAsync(model.PessoaId, model.EmpresaId) is not null)
        {
            throw new InvalidOperationException("Esse vínculo entre pessoa e empresa já está cadastrado.");
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

        Aplicar(model, lead);
        lead.DataVerif = DateTime.UtcNow;
        await _repository.SalvarAlteracoesAsync();
    }

    private static void Aplicar(LeadFormViewModel model, PessoasEmpresas lead)
    {
        lead.Cargo = Limpar(model.Cargo);
        lead.Departamento = Limpar(model.Departamento);
        lead.Senioridade = Limpar(model.Senioridade);
        lead.StatusVinculo = Limpar(model.StatusVinculo);
        lead.StatusLead = Limpar(model.StatusLead);
        lead.NivelConfianca = model.NivelConfianca;
        lead.FonteVinculo = Limpar(model.FonteVinculo);
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
