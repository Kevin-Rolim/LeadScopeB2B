using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Admin)]
public class FontesController(FonteDadosService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListarAsync());
    [HttpPost]
    public async Task<IActionResult> Create(FonteDados model)
    {
        await service.CadastrarAsync(model);
        TempData["Sucesso"] = "Fonte cadastrada.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo) { await service.AlterarAtivoAsync(id, ativo); return RedirectToAction(nameof(Index)); }
}
