using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RevisoesController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public RevisoesController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/revisoes?usuarioId=...
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? usuarioId)
    {
        var query = _context.Revisoes.AsQueryable();

        if (usuarioId.HasValue)
            query = query.Where(r => r.UsuarioId == usuarioId.Value);

        var revisoes = await query.OrderByDescending(r => r.DataHora).ToListAsync();
        return Ok(revisoes);
    }

    // GET: api/revisoes/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var revisao = await _context.Revisoes.FindAsync(id);
        if (revisao == null)
            return NotFound(new { mensagem = "Revisão não encontrada." });

        return Ok(revisao);
    }

    // GET: api/revisoes/vinculo/5/10 (Histórico de revisões daquele lead)
    [HttpGet("vinculo/{pessoaId:int}/{empresaId:int}")]
    public async Task<IActionResult> ListarRevisoesDoVinculo(int pessoaId, int empresaId)
    {
        var revisoes = await _context.Revisoes
            .Where(r => r.PessoaId == pessoaId && r.EmpresaId == empresaId)
            .OrderByDescending(r => r.DataHora)
            .ToListAsync();

        return Ok(revisoes);
    }

    // POST: api/revisoes
    [HttpPost]
    public async Task<IActionResult> RegistrarRevisao([FromBody] CriarRevisaoDto dto)
    {
        var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == dto.UsuarioId);
        if (!usuarioExiste)
            return BadRequest(new { mensagem = "Usuário analista não encontrado." });

        var vinculo = await _context.Pessoas_empresas
            .FirstOrDefaultAsync(pe => pe.PessoaId == dto.PessoaId && pe.EmpresaId == dto.EmpresaId);

        if (vinculo == null)
            return BadRequest(new { mensagem = "O vínculo entre a pessoa e a empresa informadas não existe." });

        var revisao = new Revisoes
        {
            Decisao = dto.Decisao,
            Comentario = dto.Comentario,
            DataHora = DateTime.UtcNow,
            UsuarioId = dto.UsuarioId,
            PessoaId = dto.PessoaId,
            EmpresaId = dto.EmpresaId
        };

        _context.Revisoes.Add(revisao);

        // Atualiza a data da última revisão no vínculo do lead
        vinculo.DataRevisao = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = revisao.Id }, revisao);
    }
}

public record CriarRevisaoDto(
    int UsuarioId, 
    int PessoaId, 
    int EmpresaId, 
    string Decisao, 
    string? Comentario);