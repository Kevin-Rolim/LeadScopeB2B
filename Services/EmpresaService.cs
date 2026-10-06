using LeadScopeB2B.Repositories.Interfaces;

namespace LeadScopeB2B.Services;

public class EmpresaService
{
    private readonly IEmpresaRepository _repository;

    public EmpresaService(IEmpresaRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EmpresaListViewModel>> ListarAsync()
    {
        var empresas = await _repository.ListarAsync();
        return empresas.Select(e => new EmpresaListViewModel
        {
            Id = e.Id,
            RazaoSocial = e.RazaoSocial,
            NomeFantasia = e.NomeFantasia,
            Cnpj = e.Cnpj,
            Segmento = e.Segmento,
            Cidade = e.Cidade,
            Estado = e.Estado,
            Status = e.Status
        }).ToList();
    }

    public async Task<EmpresaDetailsViewModel?> ObterDetalhesAsync(int id)
    {
        var empresa = await _repository.ObterPorIdAsync(id);
        return empresa is null ? null : MapearDetalhes(empresa);
    }

    public async Task<EmpresaFormViewModel?> ObterParaEdicaoAsync(int id)
    {
        var empresa = await _repository.ObterPorIdAsync(id);
        return empresa is null ? null : MapearFormulario(empresa);
    }

    public async Task CadastrarAsync(EmpresaFormViewModel model)
    {
        await ValidarCnpjAsync(model.Cnpj);
        var agora = DateTime.UtcNow;
        var empresa = new Empresas
        {
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        Aplicar(model, empresa);
        await _repository.AdicionarAsync(empresa);
    }

    public async Task EditarAsync(EmpresaFormViewModel model)
    {
        var empresa = await _repository.ObterPorIdAsync(model.Id)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        await ValidarCnpjAsync(model.Cnpj, model.Id);
        Aplicar(model, empresa);
        empresa.DataAtualizacao = DateTime.UtcNow;
        await _repository.SalvarAlteracoesAsync();
    }

    private async Task ValidarCnpjAsync(string? cnpj, int? empresaId = null)
    {
        if (!string.IsNullOrWhiteSpace(cnpj) &&
            await _repository.CnpjExisteAsync(cnpj.Trim(), empresaId))
        {
            throw new InvalidOperationException("Já existe uma empresa cadastrada com este CNPJ.");
        }
    }

    private static void Aplicar(EmpresaFormViewModel model, Empresas empresa)
    {
        empresa.RazaoSocial = model.RazaoSocial.Trim();
        empresa.NomeFantasia = Limpar(model.NomeFantasia);
        empresa.Cnpj = Limpar(model.Cnpj);
        empresa.UrlSite = Limpar(model.UrlSite);
        empresa.Segmento = Limpar(model.Segmento);
        empresa.Porte = Limpar(model.Porte);
        empresa.Cidade = Limpar(model.Cidade);
        empresa.Estado = Limpar(model.Estado)?.ToUpperInvariant();
        empresa.Email = Limpar(model.Email)?.ToLowerInvariant();
        empresa.Telefone = Limpar(model.Telefone);
        empresa.Status = model.Status.Trim();
    }

    private static EmpresaFormViewModel MapearFormulario(Empresas empresa) => new()
    {
        Id = empresa.Id,
        RazaoSocial = empresa.RazaoSocial,
        NomeFantasia = empresa.NomeFantasia,
        Cnpj = empresa.Cnpj,
        UrlSite = empresa.UrlSite,
        Segmento = empresa.Segmento,
        Porte = empresa.Porte,
        Cidade = empresa.Cidade,
        Estado = empresa.Estado,
        Email = empresa.Email,
        Telefone = empresa.Telefone,
        Status = empresa.Status ?? "PENDENTE"
    };

    private static EmpresaDetailsViewModel MapearDetalhes(Empresas empresa) => new()
    {
        Id = empresa.Id,
        RazaoSocial = empresa.RazaoSocial,
        NomeFantasia = empresa.NomeFantasia,
        Cnpj = empresa.Cnpj,
        UrlSite = empresa.UrlSite,
        Segmento = empresa.Segmento,
        Porte = empresa.Porte,
        Cidade = empresa.Cidade,
        Estado = empresa.Estado,
        Email = empresa.Email,
        Telefone = empresa.Telefone,
        Status = empresa.Status ?? "PENDENTE",
        DataCriacao = empresa.DataCriacao,
        DataAtualizacao = empresa.DataAtualizacao
    };

    private static string? Limpar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
