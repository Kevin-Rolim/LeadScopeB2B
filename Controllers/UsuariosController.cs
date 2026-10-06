using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeadScopeB2B.Controllers;

public class UsuariosController : Controller
{
    private readonly UsuarioService _service;

    public UsuariosController(UsuarioService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _service.ListarAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var usuario = await _service.ObterDetalhesAsync(id);
        return usuario is null ? NotFound() : View(usuario);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new UsuarioCreateViewModel();
        await _service.PrepararFormularioAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await _service.PrepararFormularioAsync(model);
            return View(model);
        }

        try
        {
            await _service.CadastrarAsync(model);
            TempData["Sucesso"] = "Usuário cadastrado com sucesso.";
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
    public async Task<IActionResult> Edit(int id)
    {
        var usuario = await _service.ObterParaEdicaoAsync(id);
        return usuario is null ? NotFound() : View(usuario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UsuarioEditViewModel model)
    {
        if (id != model.Id)
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
            await _service.EditarAsync(model);
            TempData["Sucesso"] = "Usuário atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await _service.PrepararFormularioAsync(model);
            return View(model);
        }
    }
}
