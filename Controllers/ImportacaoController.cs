using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LeadScopeB2B.Controllers;

[Authorize(Policy = Acesso.Edicao)]
public class ImportacaoController(ImportacaoCsvService service, AuditoriaService auditoria) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new List<ResultadoCsv>());

    [HttpPost, RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Index(IFormFile? arquivo, string tipo)
    {
        if (arquivo is null || arquivo.Length == 0 || arquivo.Length > 1024 * 1024)
        { ModelState.AddModelError("", "Selecione um CSV de até 1 MiB."); return View(new List<ResultadoCsv>()); }
        try
        {
            using var stream = arquivo.OpenReadStream();
            var resultado = await service.ImportarAsync(stream, tipo);
            await auditoria.RegistrarAsync(User.UsuarioId(), "IMPORTACAO_CSV", $"{tipo}: {resultado.Count(r => r.Sucesso)} cadastrados, {resultado.Count(r => !r.Sucesso)} erros.");
            return View(resultado);
        }
        catch (Exception ex) when (ex is System.ComponentModel.DataAnnotations.ValidationException or System.Text.DecoderFallbackException or Microsoft.VisualBasic.FileIO.MalformedLineException)
        { ModelState.AddModelError("", "CSV inválido: " + ex.Message); return View(new List<ResultadoCsv>()); }
    }
}
