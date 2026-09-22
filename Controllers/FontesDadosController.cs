using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FontesDadosController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public FontesDadosController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/fontesdados
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var fontes = await _context.Fontes_dados
            .Where(f => f.DataHoraDel == null)
            .ToListAsync();

        return Ok(fontes);
    }

    // GET: api/fontesdados/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var fonte = await _context.Fontes_dados
            .FirstOrDefaultAsync(f => f.Id == id && f.DataHoraDel == null);

        if (fonte == null)
            return NotFound(new { mensagem = "Fonte de dados não encontrada." });

        return Ok(fonte);
    }

    // POST: api/fontesdados
    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] Fontes_dados fonte)
    {
        fonte.DataHoraMod = DateTime.UtcNow;
        fonte.DataHoraDel = null;

        _context.Fontes_dados.Add(fonte);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = fonte.Id }, fonte);
    }

    // PUT: api/fontesdados/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] Fontes_dados fonteAtualizada)
    {
        var fonte = await _context.Fontes_dados.FirstOrDefaultAsync(f => f.Id == id && f.DataHoraDel == null);
        if (fonte == null)
            return NotFound(new { mensagem = "Fonte de dados não encontrada." });

        fonte.Nome = fonteAtualizada.Nome;
        fonte.Tipo = fonteAtualizada.Tipo;
        fonte.Url = fonteAtualizada.Url;
        fonte.Confiabilidade = fonteAtualizada.Confiabilidade;
        fonte.DataHoraMod = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH: api/fontesdados/5/confiabilidade
    [HttpPatch("{id:int}/confiabilidade")]
    public async Task<IActionResult> AtualizarConfiabilidade(int id, [FromBody] decimal confianca)
    {
        var fonte = await _context.Fontes_dados.FirstOrDefaultAsync(f => f.Id == id && f.DataHoraDel == null);
        if (fonte == null)
            return NotFound(new { mensagem = "Fonte de dados não encontrada." });

        fonte.Confiabilidade = confianca;
        fonte.DataHoraMod = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/fontesdados/5 (Soft Delete)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var fonte = await _context.Fontes_dados.FirstOrDefaultAsync(f => f.Id == id && f.DataHoraDel == null);
        if (fonte == null)
            return NotFound(new { mensagem = "Fonte de dados não encontrada." });

        fonte.DataHoraDel = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/fontesdados/dados-coletados?pessoaId=...&empresaId=...&campo=...
    [HttpGet("dados-coletados")]
    public async Task<IActionResult> RastrearDados(
        [FromQuery] int? pessoaId, 
        [FromQuery] int? empresaId, 
        [FromQuery] string? campo)
    {
        var query = _context.Dados_coletados.AsQueryable();

        if (pessoaId.HasValue)
            query = query.Where(d => d.PessoaId == pessoaId.Value);

        if (empresaId.HasValue)
            query = query.Where(d => d.EmpresaId == empresaId.Value);

        if (!string.IsNullOrWhiteSpace(campo))
            query = query.Where(d => d.Campo == campo);

        var dados = await query.ToListAsync();
        return Ok(dados);
    }

    // POST: api/fontesdados/dados-coletados
    [HttpPost("dados-coletados")]
    public async Task<IActionResult> RegistrarDadoColetado([FromBody] Dados_coletados dado)
    {
        // Validação da regra de integridade CHECK do banco
        bool ehValido = (dado.PessoaId.HasValue && !dado.EmpresaId.HasValue) 
                     || (!dado.PessoaId.HasValue && dado.EmpresaId.HasValue);

        if (!ehValido)
            return BadRequest(new { mensagem = "O dado coletado deve pertencer exclusivamente a uma Pessoa OU a uma Empresa." });

        var fonteExiste = await _context.Fontes_dados.AnyAsync(f => f.Id == dado.FonteDadosId);
        if (!fonteExiste)
            return BadRequest(new { mensagem = "Fonte de dados informada não existe." });

        _context.Dados_coletados.Add(dado);
        await _context.SaveChangesAsync();

        return Ok(dado);
    }
}