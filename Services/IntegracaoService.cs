using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Integration;
using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class IntegracaoService(IntegracaoRepository repository, FonteDadosService fontes)
{
    public async Task<List<DadosColetados>> ListarAsync()
    {
        var dados = await repository.ListarAsync();
        var origens = (await fontes.ListarAsync()).ToDictionary(f => f.Id);
        foreach (var dado in dados)
            if (origens.TryGetValue(dado.FonteDadosId, out var fonte)) dado.FonteDados = fonte;
        return dados;
    }
    public async Task<DadosColetados?> ObterDaFonteAsync(int id, int fonteId)
    {
        var dado = await repository.ObterAsync(id);
        return dado?.FonteDadosId == fonteId ? dado : null;
    }
    public async Task<int> ReceberAsync(DadoRecebido input, int fonteId, int usuarioId)
    {
        CadastroRules.Validar(input);
        if (!await fontes.EstaAtivaAsync(fonteId)) throw new ConflitoException("A fonte de dados não está ativa.");
        var valor = Normalizar(input.PessoaId.HasValue, input.Campo, input.Valor);
        return await repository.AdicionarAsync(new DadosColetados { PessoaId = input.PessoaId, EmpresaId = input.EmpresaId, Campo = input.Campo.Trim(), Valor = valor, NivelConfianca = input.NivelConfianca, FonteDadosId = fonteId }, usuarioId);
    }
    public async Task<RevisaoColeta?> ObterRevisaoColetaAsync(int id)
    {
        var revisao = await repository.ObterRevisaoColetaAsync(id);
        if (revisao is not null)
            revisao.Dado.FonteDados = (await fontes.ListarAsync()).Single(f => f.Id == revisao.Dado.FonteDadosId);
        return revisao;
    }
    public Task RevisarColetaAsync(int id, string? valorEsperado, bool aprovar, int usuarioId) =>
        repository.RevisarColetaAsync(id, valorEsperado, aprovar, usuarioId);

    internal static string Normalizar(bool pessoa, string campo, string valor)
    {
        campo = campo.Trim();
        valor = valor.Trim();
        var tamanho = (pessoa, campo) switch
        {
            (true, "Nome") => 150,
            (true, "Email") => 150,
            (true, "Telefone") => 20,
            (true, "UrlLinkedIn") => 255,
            (true, "Origem") => 100,
            (false, "RazaoSocial") => 200,
            (false, "NomeFantasia") => 200,
            (false, "Cnpj") => 18,
            (false, "UrlSite") => 255,
            (false, "Segmento") => 100,
            (false, "Porte") => 50,
            (false, "Cidade") => 100,
            (false, "Estado") => 2,
            (false, "Email") => 150,
            (false, "Telefone") => 20,
            _ => throw new ValidationException("Campo não permitido para este destino.")
        };
        if (valor.Length == 0 || valor.Length > tamanho) throw new ValidationException($"Valor deve conter de 1 a {tamanho} caracteres.");
        if (campo == "Cnpj") return CadastroRules.Cnpj(valor)!;
        if (campo is "UrlSite" or "UrlLinkedIn") return CadastroRules.Url(valor)!.TrimEnd('/');
        if (campo == "Email")
        {
            if (!new EmailAddressAttribute().IsValid(valor)) throw new ValidationException("E-mail inválido.");
            return valor.ToLowerInvariant();
        }
        if (campo == "Estado" && !System.Text.RegularExpressions.Regex.IsMatch(valor, "^[a-zA-Z]{2}$")) throw new ValidationException("Use a sigla do estado.");
        return campo == "Estado" ? valor.ToUpperInvariant() : valor;
    }
}
