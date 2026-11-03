using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LeadScopeB2B.Controllers;

public class HomeController(LeadScopeB2B.Services.EmpresaService empresas, LeadScopeB2B.Services.PessoaService pessoas, LeadScopeB2B.Services.LeadService leads) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(new LeadScopeB2B.ViewModels.Home.OverviewViewModel { Empresas = await empresas.ListarAsync(), Pessoas = await pessoas.ListarAsync(), Leads = await leads.ListarAsync() });
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
