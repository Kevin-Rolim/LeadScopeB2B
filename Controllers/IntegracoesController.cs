using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Edicao)]
public class IntegracoesController(IntegracaoService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListarAsync());

    [HttpGet]
    public async Task<IActionResult> Revisar(int id)
    {
        var revisao = await service.ObterRevisaoColetaAsync(id);
        return revisao is null ? NotFound() : View(revisao);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revisar(int id, string? valorEsperado, bool aprovar)
    {
        var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await service.RevisarColetaAsync(id, valorEsperado, aprovar, usuarioId);
            TempData["Sucesso"] = aprovar ? "Evidência aprovada e campo atualizado." : "Evidência rejeitada; cadastro preservado.";
            var revisada = await service.ObterRevisaoColetaAsync(id);
            if (revisada?.Dado.EmpresaId is int empresaId) return RedirectToAction("Details", "Empresas", new { id=empresaId });
            if (revisada?.Dado.PessoaId is int pessoaId) return RedirectToAction("Details", "Pessoas", new { id=pessoaId });
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConflitoException ex)
        {
            ModelState.AddModelError("", ex.Message);
            var revisao = await service.ObterRevisaoColetaAsync(id);
            return revisao is null ? NotFound() : View(revisao);
        }
    }
}
