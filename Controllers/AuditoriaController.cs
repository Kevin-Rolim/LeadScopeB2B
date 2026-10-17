using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Admin)]
public class AuditoriaController(AuditoriaService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListarAsync());
}
