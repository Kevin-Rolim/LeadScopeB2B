using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeadScopeB2B.Controllers;

public class LeadsController : Controller
{
    private readonly LeadService _service;

    public LeadsController(LeadService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _service.ListarAsync());
    }

    public async Task<IActionResult> Details(int pessoaId, int empresaId)
    {
        var lead = await _service.ObterDetalhesAsync(pessoaId, empresaId);
        return lead is null ? NotFound() : View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new LeadFormViewModel();
        await _service.PrepararFormularioAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await _service.PrepararFormularioAsync(model);
            return View(model);
        }

        try
        {
            await _service.CadastrarAsync(model);
            TempData["Sucesso"] = "Lead cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await _service.PrepararFormularioAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int pessoaId, int empresaId)
    {
        var lead = await _service.ObterParaEdicaoAsync(pessoaId, empresaId);
        return lead is null ? NotFound() : View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int pessoaId, int empresaId, LeadFormViewModel model)
    {
        if (pessoaId != model.PessoaId || empresaId != model.EmpresaId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await _service.PrepararFormularioAsync(model);
            return View(model);
        }

        try
        {
            await _service.EditarAsync(pessoaId, empresaId, model);
            TempData["Sucesso"] = "Lead atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
