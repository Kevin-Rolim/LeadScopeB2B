using LeadScopeB2B.Integration;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace LeadScopeB2B.Controllers.Api;

[ApiController, Route("api"), Authorize(AuthenticationSchemes = Acesso.Api), IgnoreAntiforgeryToken, EnableRateLimiting("api")]
[Produces("application/json")]
public class CatalogoApiController(EmpresaService empresas, PessoaService pessoas, LeadService leads, CnaeService cnaes,
    IntegracaoService integracao, FonteDadosService fontes) : ControllerBase
{
    [HttpGet("empresas")]
    public async Task<IActionResult> Empresas(string? busca = null, string? status = null, string? cnae = null) => Ok(await empresas.ListarAsync(busca, status, cnae));
    [HttpGet("empresas/{id:int}")]
    public async Task<IActionResult> Empresa(int id)
    {
        var model = await empresas.ObterDetalhesAsync(id);
        if (model is null) return NotFound();
        model.Cnaes = await cnaes.ListarDaEmpresaAsync(id);
        return Ok(model);
    }
    [HttpGet("pessoas")]
    public async Task<IActionResult> Pessoas(string? busca = null, string? status = null) => Ok(await pessoas.ListarAsync(busca, status));
    [HttpGet("pessoas/{id:int}")]
    public async Task<IActionResult> Pessoa(int id)
    {
        var model = await pessoas.ObterDetalhesAsync(id);
        return model is null ? NotFound() : Ok(model);
    }
    [HttpGet("leads")]
    public async Task<IActionResult> Leads(string? busca = null, string? status = null, int? pessoaId = null, int? empresaId = null, string? cnae = null) => Ok(await leads.ListarAsync(busca, status, pessoaId, empresaId, cnae));
    [HttpGet("leads/{pessoaId:int}/{empresaId:int}")]
    public async Task<IActionResult> Lead(int pessoaId, int empresaId)
    {
        var model = await leads.ObterDetalhesAsync(pessoaId, empresaId);
        if (model is null) return NotFound();
        return Ok(new { model.PessoaId, model.EmpresaId, model.Pessoa, model.Empresa, model.Cargo, model.Departamento, model.Senioridade, model.StatusVinculo, model.StatusLead, model.NivelConfianca, model.FonteVinculo, model.DataRevisao, model.DataVerificacao });
    }
    [HttpGet("cnaes")]
    public async Task<IActionResult> Cnaes(string? busca = null) => Ok(await cnaes.ListarAsync(busca));
    [HttpPost("integracoes/dados"), Authorize(Policy = "EnvioIntegracao"), RequestSizeLimit(32768)]
    public async Task<IActionResult> Receber(DadoRecebido model)
    {
        var fonte = int.Parse(User.FindFirst("fonte")!.Value);
        var id = await integracao.ReceberAsync(model, fonte, User.UsuarioId());
        return AcceptedAtAction(nameof(Dado), new { id }, new { id, status = "PENDENTE" });
    }
    [HttpGet("integracoes/dados/{id:int}")]
    public async Task<IActionResult> Dado(int id)
    {
        var dado = await integracao.ObterDaFonteAsync(id, int.Parse(User.FindFirst("fonte")!.Value));
        if (dado is null) return NotFound();
        return Ok(new { dado.Id, dado.PessoaId, dado.EmpresaId, dado.Campo, dado.Valor, dado.ValorNormalizado, dado.StatusValidacao, dado.NivelConfianca, dado.DataColeta });
    }
    [HttpPost("integracoes/leads"), Authorize(Policy = "EnvioIntegracao"), RequestSizeLimit(32768)]
    public async Task<IActionResult> ReceberLead(LeadRecebido model)
    {
        var fonte = int.Parse(User.FindFirst("fonte")!.Value);
        if (!await fontes.EstaAtivaAsync(fonte)) throw new ConflitoException("Fonte inativa.");
        await leads.CadastrarAsync(new LeadFormViewModel { PessoaId = model.PessoaId, EmpresaId = model.EmpresaId, Cargo = model.Cargo, Departamento = model.Departamento, Senioridade = model.Senioridade, NivelConfianca = model.NivelConfianca, StatusLead = "NOVO", StatusVinculo = "INCERTO", FonteVinculo = $"Fonte {fonte}" });
        return CreatedAtAction(nameof(Lead), new { model.PessoaId, model.EmpresaId }, new { model.PessoaId, model.EmpresaId, status = "NOVO" });
    }
}
