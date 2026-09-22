using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeadsController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public LeadsController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/leads?cargo=...&senioridade=...&statusLead=...
    [HttpGet]
    public async Task<IActionResult> ListarLeads(
        [FromQuery] string? cargo,
        [FromQuery] string? senioridade,
        [FromQuery] string? statusLead)
    {
        var query = _context.Pessoas_empresas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(cargo))
            query = query.Where(pe => pe.Cargo != null && pe.Cargo.Contains(cargo));

        if (!string.IsNullOrWhiteSpace(senioridade))
            query = query.Where(pe => pe.Senioridade == senioridade);

        if (!string.IsNullOrWhiteSpace(statusLead))
            query = query.Where(pe => pe.StatusLead == statusLead);

        var leads = await query.ToListAsync();
        return Ok(leads);
    }

    // GET: api/leads/empresa/5 (Organograma da empresa)
    [HttpGet("empresa/{empresaId:int}")]
    public async Task<IActionResult> ObterOrganogramaEmpresa(int empresaId)
    {
        var organograma = await _context.Pessoas_empresas
            .Where(pe => pe.EmpresaId == empresaId)
            .Join(_context.Pessoas,
                pe => pe.PessoaId,
                p => p.Id,
                (pe, p) => new
                {
                    pe.PessoaId,
                    pe.EmpresaId,
                    p.Nome,
                    p.Email,
                    p.Telefone,
                    p.UrlLinkedin,
                    pe.Cargo,
                    pe.Departamento,
                    pe.Senioridade,
                    pe.StatusVinculo,
                    pe.StatusLead,
                    pe.NivelConfianca
                })
            .ToListAsync();

        return Ok(organograma);
    }

    // GET: api/leads/5/10
    [HttpGet("{pessoaId:int}/{empresaId:int}")]
    public async Task<IActionResult> ObterVinculo(int pessoaId, int empresaId)
    {
        var vinculo = await _context.Pessoas_empresas
            .FirstOrDefaultAsync(pe => pe.PessoaId == pessoaId && pe.EmpresaId == empresaId);

        if (vinculo == null)
            return NotFound(new { mensagem = "Vínculo lead-empresa não encontrado." });

        return Ok(vinculo);
    }

    // POST: api/leads
    [HttpPost]
    public async Task<IActionResult> CriarVinculo([FromBody] Pessoas_empresas novoVinculo)
    {
        var pessoaExiste = await _context.Pessoas.AnyAsync(p => p.Id == novoVinculo.PessoaId);
        var empresaExiste = await _context.Empresas.AnyAsync(e => e.Id == novoVinculo.EmpresaId);

        if (!pessoaExiste || !empresaExiste)
            return BadRequest(new { mensagem = "Pessoa ou Empresa informada não existe." });

        var jaExiste = await _context.Pessoas_empresas
            .AnyAsync(pe => pe.PessoaId == novoVinculo.PessoaId && pe.EmpresaId == novoVinculo.EmpresaId);

        if (jaExiste)
            return BadRequest(new { mensagem = "Vínculo entre esta Pessoa e Empresa já existe." });

        novoVinculo.DataVerificacao = DateTime.UtcNow;
        _context.Pessoas_empresas.Add(novoVinculo);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterVinculo), 
            new { pessoaId = novoVinculo.PessoaId, empresaId = novoVinculo.EmpresaId }, novoVinculo);
    }

    // PUT: api/leads/5/10
    [HttpPut("{pessoaId:int}/{empresaId:int}")]
    public async Task<IActionResult> AtualizarLead(int pessoaId, int empresaId, [FromBody] AtualizarLeadDto dto)
    {
        var vinculo = await _context.Pessoas_empresas
            .FirstOrDefaultAsync(pe => pe.PessoaId == pessoaId && pe.EmpresaId == empresaId);

        if (vinculo == null)
            return NotFound(new { mensagem = "Vínculo não encontrado." });

        vinculo.Cargo = dto.Cargo;
        vinculo.Departamento = dto.Departamento;
        vinculo.Senioridade = dto.Senioridade;
        vinculo.StatusVinculo = dto.StatusVinculo;
        vinculo.StatusLead = dto.StatusLead;
        vinculo.FonteVinculo = dto.FonteVinculo;
        vinculo.DataVerificacao = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH: api/leads/5/10/status
    [HttpPatch("{pessoaId:int}/{empresaId:int}/status")]
    public async Task<IActionResult> AtualizarStatusLead(int pessoaId, int empresaId, [FromBody] AtualizarStatusLeadDto dto)
    {
        var vinculo = await _context.Pessoas_empresas
            .FirstOrDefaultAsync(pe => pe.PessoaId == pessoaId && pe.EmpresaId == empresaId);

        if (vinculo == null)
            return NotFound(new { mensagem = "Vínculo não encontrado." });

        vinculo.StatusLead = dto.StatusLead;
        if (!string.IsNullOrWhiteSpace(dto.StatusVinculo))
            vinculo.StatusVinculo = dto.StatusVinculo;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH: api/leads/5/10/confianca
    [HttpPatch("{pessoaId:int}/{empresaId:int}/confianca")]
    public async Task<IActionResult> AtualizarConfianca(int pessoaId, int empresaId, [FromBody] decimal nivelConfianca)
    {
        var vinculo = await _context.Pessoas_empresas
            .FirstOrDefaultAsync(pe => pe.PessoaId == pessoaId && pe.EmpresaId == empresaId);

        if (vinculo == null)
            return NotFound(new { mensagem = "Vínculo não encontrado." });

        vinculo.NivelConfianca = nivelConfianca;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public record AtualizarLeadDto(
    string? Cargo, 
    string? Departamento, 
    string? Senioridade, 
    string? StatusVinculo, 
    string? StatusLead, 
    string? FonteVinculo);

public record AtualizarStatusLeadDto(string StatusLead, string? StatusVinculo);