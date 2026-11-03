using LeadScopeB2B.Security;
using Microsoft.AspNetCore.Authorization;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeadScopeB2B.Controllers;

public class PessoasController : Controller
{
    private readonly PessoaService _service;

    public PessoasController(PessoaService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(string? busca, string? status)
    {
        return View(await _service.ListarAsync(busca, status));
    }

    public async Task<IActionResult> Details(int id)
    {
        var pessoa = await _service.ObterDetalhesAsync(id);
        return pessoa is null ? NotFound() : View(pessoa);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new PessoaFormViewModel());
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PessoaFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _service.CadastrarAsync(model);
        TempData["Sucesso"] = "Pessoa cadastrada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var pessoa = await _service.ObterParaEdicaoAsync(id);
        return pessoa is null ? NotFound() : View(pessoa);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PessoaFormViewModel model)
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
            TempData["Sucesso"] = "Pessoa atualizada com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarBloqueio(int id, bool bloquear)
    {
        try
        {
            await _service.AlterarBloqueioAsync(id, bloquear);
            TempData["Sucesso"] = bloquear ? "Pessoa bloqueada." : "Pessoa desbloqueada.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var pessoa = await _service.ObterDetalhesAsync(id);
        return pessoa is null ? NotFound() : View(pessoa);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarDelete(int id)
    {
        try
        {
            await _service.ExcluirAsync(id);
            TempData["Sucesso"] = "Pessoa removida com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
