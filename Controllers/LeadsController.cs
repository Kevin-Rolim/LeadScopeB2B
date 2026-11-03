using LeadScopeB2B.Security;
using Microsoft.AspNetCore.Authorization;
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

    public async Task<IActionResult> Index(string? busca, string? status, int? pessoaId, int? empresaId, string? cnae)
    {
        return View(await _service.ListarAsync(busca, status, pessoaId, empresaId, cnae));
    }

    public async Task<IActionResult> Details(int pessoaId, int empresaId)
    {
        var lead = await _service.ObterDetalhesAsync(pessoaId, empresaId);
        return lead is null ? NotFound() : View(lead);
    }

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new LeadFormViewModel();
        await _service.PrepararFormularioAsync(model);
        return View(model);
    }

    [Authorize(Policy = Acesso.Edicao)]
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

    [Authorize(Policy = Acesso.Edicao)]
    [HttpGet]
    public async Task<IActionResult> Edit(int pessoaId, int empresaId)
    {
        var lead = await _service.ObterParaEdicaoAsync(pessoaId, empresaId);
        return lead is null ? NotFound() : View(lead);
    }

    [Authorize(Policy = Acesso.Edicao)]
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
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Excluir(int pessoaId, int empresaId)
    {
        await _service.ExcluirAsync(pessoaId, empresaId);
        TempData["Sucesso"] = "Registro removido da lista de ativos.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Revisar(int pessoaId, int empresaId, string decisao, string? comentario)
    {
        await _service.RevisarAsync(pessoaId, empresaId, User.UsuarioId(), decisao, comentario);
        return RedirectToAction(nameof(Details), new { pessoaId, empresaId });
    }
    [HttpPost, Authorize(Policy = Acesso.Edicao)]
    public async Task<IActionResult> Exportar(string[] selecionados, [FromServices] ExportacaoService exportacao)
    {
        var csv = await exportacao.ExportarAsync(selecionados, User.UsuarioId());
        return File(csv, "text/csv; charset=utf-8", "leads.csv");
    }
}
