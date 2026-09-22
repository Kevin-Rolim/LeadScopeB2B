using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PessoasController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public PessoasController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/pessoas?status=...&origem=...&termo=...
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? status,
        [FromQuery] string? origem,
        [FromQuery] string? termo)
    {
        var query = _context.Pessoas
            .Where(p => p.DataDel == null) // Apenas não deletados
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(origem))
            query = query.Where(p => p.Origem == origem);

        if (!string.IsNullOrWhiteSpace(termo))
            query = query.Where(p => p.Nome.Contains(termo) || (p.Email != null && p.Email.Contains(termo)));

        var pessoas = await query.ToListAsync();
        return Ok(pessoas);
    }

    // GET: api/pessoas/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Id == id && p.DataDel == null);
        if (pessoa == null)
            return NotFound(new { mensagem = "Contato não encontrado ou excluído." });

        return Ok(pessoa);
    }

    // POST: api/pessoas
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] Pessoas pessoa)
    {
        pessoa.DataColeta = DateTime.UtcNow;
        pessoa.DataBloqueio = null;
        pessoa.DataDel = null;

        _context.Pessoas.Add(pessoa);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = pessoa.Id }, pessoa);
    }

    // PUT: api/pessoas/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] Pessoas pessoaAtualizada)
    {
        var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Id == id && p.DataDel == null);
        if (pessoa == null)
            return NotFound(new { mensagem = "Contato não encontrado." });

        pessoa.Nome = pessoaAtualizada.Nome;
        pessoa.Email = pessoaAtualizada.Email;
        pessoa.Telefone = pessoaAtualizada.Telefone;
        pessoa.UrlLinkedin = pessoaAtualizada.UrlLinkedin;
        pessoa.Status = pessoaAtualizada.Status;
        pessoa.Origem = pessoaAtualizada.Origem;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH: api/pessoas/5/bloquear
    [HttpPatch("{id:int}/bloquear")]
    public async Task<IActionResult> AlterarBloqueio(int id, [FromBody] bool bloquear)
    {
        var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Id == id && p.DataDel == null);
        if (pessoa == null)
            return NotFound(new { mensagem = "Contato não encontrado." });

        pessoa.DataBloqueio = bloquear ? DateTime.UtcNow : null;
        pessoa.Status = bloquear ? "Bloqueado" : "Ativo";

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/pessoas/5 (Soft Delete)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Id == id && p.DataDel == null);
        if (pessoa == null)
            return NotFound(new { mensagem = "Contato não encontrado." });

        pessoa.DataDel = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/pessoas/5/dados-coletados
    [HttpGet("{id:int}/dados-coletados")]
    public async Task<IActionResult> ObterHistoricoColetas(int id)
    {
        var historico = await _context.Dados_coletados
            .Where(d => d.PessoaId == id)
            .ToListAsync();

        return Ok(historico);
    }
}