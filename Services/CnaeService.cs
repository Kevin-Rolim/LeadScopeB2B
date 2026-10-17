using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LeadScopeB2B.Repositories;
namespace LeadScopeB2B.Services;

public class CnaeService(CnaeRepository repository)
{
    public Task<List<Cnaes>> ListarAsync(string? busca = null) => repository.ListarAsync(CadastroRules.Limpar(busca));
    public Task CadastrarAsync(Cnaes model) { model.Numero = Normalizar(model.Numero); model.Descricao = model.Descricao?.Trim() ?? ""; CadastroRules.Validar(model); return repository.AdicionarAsync(model); }
    public Task<List<CnaesEmpresas>> ListarDaEmpresaAsync(int empresaId) => repository.ListarDaEmpresaAsync(empresaId);
    public Task AssociarAsync(int empresaId, string numero, bool principal) => repository.AssociarAsync(empresaId, Normalizar(numero), principal);
    public Task RemoverAsync(int empresaId, string numero) => repository.RemoverAsync(empresaId, Normalizar(numero));
    public static string Normalizar(string? numero)
    {
        if (numero is null || !Regex.IsMatch(numero, @"^[0-9./\-\s]+$")) throw new ValidationException("CNAE inválido.");
        numero = Regex.Replace(numero, @"\D", "");
        if (numero.Length != 7) throw new ValidationException("CNAE deve conter sete dígitos.");
        return numero;
    }
}
