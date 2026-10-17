using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

public class CnaesController(CnaeService service) : Controller
{
    public async Task<IActionResult> Index(string? busca) => View(await service.ListarAsync(busca));
    [HttpPost, Authorize(Policy = Acesso.Admin)]
    public async Task<IActionResult> Create(Cnaes model)
    {
        await service.CadastrarAsync(model);
        TempData["Sucesso"] = "CNAE cadastrado.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Associar(int empresaId, string numero, bool principal)
    {
        await service.AssociarAsync(empresaId, numero, principal);
        return RedirectToAction("Details", "Empresas", new { id = empresaId });
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Remover(int empresaId, string numero)
    {
        await service.RemoverAsync(empresaId, numero);
        return RedirectToAction("Details", "Empresas", new { id = empresaId });
    }
}
