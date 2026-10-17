using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Edicao)]
public class IntegracoesController(IntegracaoService service) : Controller
{
    public async Task<IActionResult> Index(string? status = "PENDENTE") => View(await service.ListarAsync(CadastroRules.Limpar(status)));
    [HttpPost]
    public async Task<IActionResult> Revisar(int id, bool aprovar)
    {
        await service.RevisarAsync(id, aprovar, User.UsuarioId());
        TempData["Sucesso"] = "Evidência revisada.";
        return RedirectToAction(nameof(Index));
    }
}
