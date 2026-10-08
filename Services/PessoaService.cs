using LeadScopeB2B.Repositories.Interfaces;

namespace LeadScopeB2B.Services;

public class PessoaService
{
    private readonly IPessoaRepository _repository;

    public PessoaService(IPessoaRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PessoaListViewModel>> ListarAsync()
    {
        var pessoas = await _repository.ListarAtivasAsync();
        return pessoas.Select(p => new PessoaListViewModel
        {
            Id = p.Id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone,
            Status = p.Status,
            Origem = p.Origem,
            Bloqueada = p.DataBloqueio.HasValue
        }).ToList();
    }

    public async Task<PessoaDetailsViewModel?> ObterDetalhesAsync(int id)
    {
        var pessoa = await _repository.ObterAtivaPorIdAsync(id);
        return pessoa is null ? null : MapearDetalhes(pessoa);
    }

    public async Task<PessoaFormViewModel?> ObterParaEdicaoAsync(int id)
    {
        var pessoa = await _repository.ObterAtivaPorIdAsync(id);
        return pessoa is null ? null : MapearFormulario(pessoa);
    }

    public async Task CadastrarAsync(PessoaFormViewModel model)
    {
        var pessoa = new Pessoas
        {
            DataColeta = DateTime.UtcNow
        };
        Aplicar(model, pessoa);
        await _repository.AdicionarAsync(pessoa);
    }

    public async Task EditarAsync(PessoaFormViewModel model)
    {
        var pessoa = await _repository.ObterAtivaPorIdAsync(model.Id)
            ?? throw new KeyNotFoundException("Pessoa não encontrada.");

        Aplicar(model, pessoa);
        await _repository.SalvarAlteracoesAsync();
    }

    public async Task AlterarBloqueioAsync(int id, bool bloquear)
    {
        var pessoa = await _repository.ObterAtivaPorIdAsync(id)
            ?? throw new KeyNotFoundException("Pessoa não encontrada.");

        pessoa.DataBloqueio = bloquear ? DateTime.UtcNow : null;
        pessoa.Status = bloquear ? "Bloqueado" : "Ativo";
        await _repository.SalvarAlteracoesAsync();
    }

    public async Task ExcluirAsync(int id)
    {
        var pessoa = await _repository.ObterAtivaPorIdAsync(id)
            ?? throw new KeyNotFoundException("Pessoa não encontrada.");

        pessoa.DataDel = DateTime.UtcNow;
        await _repository.SalvarAlteracoesAsync();
    }

    private static void Aplicar(PessoaFormViewModel model, Pessoas pessoa)
    {
        pessoa.Nome = model.Nome.Trim();
        pessoa.Email = Limpar(model.Email)?.ToLowerInvariant();
        pessoa.Telefone = Limpar(model.Telefone);
        pessoa.UrlLinkedin = Limpar(model.UrlLinkedin);
        pessoa.Status = model.Status.Trim();
        pessoa.Origem = Limpar(model.Origem);
    }

    private static PessoaFormViewModel MapearFormulario(Pessoas pessoa) => new()
    {
        Id = pessoa.Id,
        Nome = pessoa.Nome,
        Email = pessoa.Email,
        Telefone = pessoa.Telefone,
        UrlLinkedin = pessoa.UrlLinkedin,
        Status = pessoa.Status ?? "PENDENTE",
        Origem = pessoa.Origem
    };

    private static PessoaDetailsViewModel MapearDetalhes(Pessoas pessoa) => new()
    {
        Id = pessoa.Id,
        Nome = pessoa.Nome,
        Email = pessoa.Email,
        Telefone = pessoa.Telefone,
        UrlLinkedin = pessoa.UrlLinkedin,
        Status = pessoa.Status ?? "PENDENTE",
        Origem = pessoa.Origem,
        DataColeta = pessoa.DataColeta,
        DataBloqueio = pessoa.DataBloqueio
    };

    private static string? Limpar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
