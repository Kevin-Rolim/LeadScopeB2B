using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Integration;
using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class IntegracaoService(IntegracaoRepository repository, FonteDadosService fontes)
{
    public Task<List<DadosColetados>> ListarAsync(string? status = "PENDENTE") => repository.ListarAsync(status);
    public async Task<DadosColetados?> ObterDaFonteAsync(int id, int fonteId)
    {
        var dado = await repository.ObterAsync(id);
        return dado?.FonteDadosID == fonteId ? dado : null;
    }
    public async Task<int> ReceberAsync(DadoRecebido input, int fonteId, int usuarioId)
    {
        CadastroRules.Validar(input);
        if (!await fontes.EstaAtivaAsync(fonteId)) throw new ConflitoException("A fonte de dados não está ativa.");
        var valor = Normalizar(input.PessoaId.HasValue, input.Campo, input.Valor);
        return await repository.AdicionarAsync(new DadosColetados { PessoaId = input.PessoaId, EmpresaId = input.EmpresaId, Campo = input.Campo.Trim().ToLowerInvariant(), Valor = input.Valor.Trim(), ValorNormalizado = valor, NivelConfianca = input.NivelConfianca, EvidenciaUrl = CadastroRules.Url(input.EvidenciaUrl), FonteDadosID = fonteId }, usuarioId);
    }
    public async Task RevisarAsync(int id, bool aprovar, int usuarioId)
    {
        var dado = await repository.ObterAsync(id) ?? throw new KeyNotFoundException("Dado coletado não encontrado.");
        var valor = Normalizar(dado.PessoaId.HasValue, dado.Campo, dado.Valor);
        await repository.RevisarAsync(dado, valor, aprovar, usuarioId);
    }
    internal static string Normalizar(bool pessoa, string campo, string valor)
    {
        campo = campo.Trim().ToLowerInvariant();
        valor = valor.Trim();
        var tamanho = (pessoa, campo) switch
        {
            (true, "nome") => 150,
            (true, "email") => 150,
            (true, "telefone") => 20,
            (true, "linkedin") => 255,
            (true, "origem") => 100,
            (false, "razao_social") => 200,
            (false, "nome_fantasia") => 200,
            (false, "cnpj") => 18,
            (false, "site") => 255,
            (false, "segmento") => 100,
            (false, "porte") => 50,
            (false, "cidade") => 100,
            (false, "estado") => 2,
            (false, "email") => 150,
            (false, "telefone") => 20,
            _ => throw new ValidationException("Campo não permitido para este destino.")
        };
        if (valor.Length == 0 || valor.Length > tamanho) throw new ValidationException($"Valor deve conter de 1 a {tamanho} caracteres.");
        if (campo == "cnpj") return CadastroRules.Cnpj(valor)!;
        if (campo is "site" or "linkedin") return CadastroRules.Url(valor)!.TrimEnd('/');
        if (campo == "email")
        {
            if (!new EmailAddressAttribute().IsValid(valor)) throw new ValidationException("E-mail inválido.");
            return valor.ToLowerInvariant();
        }
        if (campo == "estado" && !System.Text.RegularExpressions.Regex.IsMatch(valor, "^[a-zA-Z]{2}$")) throw new ValidationException("Use a sigla do estado.");
        return campo == "estado" ? valor.ToUpperInvariant() : valor;
    }
}
