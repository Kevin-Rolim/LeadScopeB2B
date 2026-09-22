using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CnaesController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public CnaesController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/cnaes?termo=tecnologia&limite=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? termo, 
        [FromQuery] int limite = 50)
    {
        var query = _context.Cnae.AsQueryable();

        if (!string.IsNullOrWhiteSpace(termo))
        {
            query = query.Where(c => 
                c.Numero.Contains(termo) || 
                c.Descricao.Contains(termo));
        }

        var resultados = await query
            .Take(limite)
            .ToListAsync();

        return Ok(resultados);
    }

    // GET: api/cnaes/6201501
    [HttpGet("{numero}")]
    public async Task<IActionResult> ObterPorNumero(string numero)
    {
        var cnae = await _context.Cnae.FindAsync(numero);
        if (cnae == null)
            return NotFound(new { mensagem = "CNAE não encontrado." });

        return Ok(cnae);
    }

    // POST: api/cnaes
    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] Cnae cnae)
    {
        var jaExiste = await _context.Cnae.AnyAsync(c => c.Numero == cnae.Numero);
        if (jaExiste)
            return BadRequest(new { mensagem = "Este código CNAE já está cadastrado." });

        _context.Cnae.Add(cnae);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorNumero), new { numero = cnae.Numero }, cnae);
    }
}