using LeadScopeB2B.Security;
using Microsoft.AspNetCore.Authorization;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeadScopeB2B.Controllers;

public class EmpresasController : Controller
{
    private readonly EmpresaService _service;
    private readonly CnaeService _cnaes;

    public EmpresasController(EmpresaService service, CnaeService cnaes)
    {
        _service = service;
        _cnaes = cnaes;
    }

    public async Task<IActionResult> Index(string? busca, string? status, string? cnae)
    {
        return View(await _service.ListarAsync(busca, status, cnae));
    }

    public async Task<IActionResult> Details(int id, string? cnaeBusca)
    {
        var empresa = await _service.ObterDetalhesAsync(id);
        if (empresa is null) return NotFound();
        empresa.Cnaes = await _cnaes.ListarDaEmpresaAsync(id);
        ViewBag.CatalogoCnaes = await _cnaes.ListarAsync(cnaeBusca);
        return View(empresa);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmpresaFormViewModel());
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmpresaFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _service.CadastrarAsync(model);
            TempData["Sucesso"] = "Empresa cadastrada com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.Cnpj), ex.Message);
            return View(model);
        }
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? cnaeBusca)
    {
        var empresa = await _service.ObterParaEdicaoAsync(id);
        if (empresa is not null) { ViewBag.Cnaes = await _cnaes.ListarDaEmpresaAsync(id); ViewBag.CatalogoCnaes = await _cnaes.ListarAsync(cnaeBusca); }
        return empresa is null ? NotFound() : View(empresa);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmpresaFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _service.EditarAsync(model);
            TempData["Sucesso"] = "Empresa atualizada com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.Cnpj), ex.Message);
            return View(model);
        }
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Excluir(int id)
    {
        await _service.ExcluirAsync(id);
        TempData["Sucesso"] = "Registro removido da lista de ativos.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> AssociarCnae(int id, string numero)
    {
        await _cnaes.AssociarAsync(id, numero);
        return RedirectToAction(nameof(Edit), new { id });
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> RemoverCnae(int id, string numero)
    {
        await _cnaes.RemoverAsync(id, numero);
        return RedirectToAction(nameof(Edit), new { id });
    }
}
