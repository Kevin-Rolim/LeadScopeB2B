using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmpresasController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public EmpresasController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/empresas?cnpj=...&estado=...&segmento=...&status=...
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? cnpj,
        [FromQuery] string? estado,
        [FromQuery] string? segmento,
        [FromQuery] string? status)
    {
        var query = _context.Empresas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(cnpj))
            query = query.Where(e => e.Cnpj != null && e.Cnpj.Contains(cnpj));

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(e => e.Estado == estado);

        if (!string.IsNullOrWhiteSpace(segmento))
            query = query.Where(e => e.Segmento != null && e.Segmento.Contains(segmento));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var empresas = await query.ToListAsync();
        return Ok(empresas);
    }

    // GET: api/empresas/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var empresa = await _context.Empresas.FindAsync(id);
        if (empresa == null)
            return NotFound(new { mensagem = "Empresa não encontrada." });

        var cnaes = await _context.Cnaes_empresas
            .Where(ce => ce.EmpresaId == id)
            .Select(ce => ce.CnaeNumero)
            .ToListAsync();

        return Ok(new { Empresa = empresa, Cnaes = cnaes });
    }

    // POST: api/empresas
    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] Empresas empresa)
    {
        if (!string.IsNullOrWhiteSpace(empresa.Cnpj))
        {
            var cnpjJaExiste = await _context.Empresas.AnyAsync(e => e.Cnpj == empresa.Cnpj);
            if (cnpjJaExiste)
                return BadRequest(new { mensagem = "Já existe uma empresa cadastrada com este CNPJ." });
        }

        empresa.DataCriacao = DateTime.UtcNow;
        empresa.DataAtualizacao = DateTime.UtcNow;

        _context.Empresas.Add(empresa);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = empresa.Id }, empresa);
    }

    // PUT: api/empresas/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] Empresas empresaAtualizada)
    {
        var empresa = await _context.Empresas.FindAsync(id);
        if (empresa == null)
            return NotFound(new { mensagem = "Empresa não encontrada." });

        empresa.RazaoSocial = empresaAtualizada.RazaoSocial;
        empresa.NomeFantasia = empresaAtualizada.NomeFantasia;
        empresa.Cnpj = empresaAtualizada.Cnpj;
        empresa.Site = empresaAtualizada.Site;
        empresa.Segmento = empresaAtualizada.Segmento;
        empresa.Porte = empresaAtualizada.Porte;
        empresa.Cidade = empresaAtualizada.Cidade;
        empresa.Estado = empresaAtualizada.Estado;
        empresa.Email = empresaAtualizada.Email;
        empresa.Telefone = empresaAtualizada.Telefone;
        empresa.Status = empresaAtualizada.Status;
        empresa.DataAtualizacao = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET: api/empresas/5/cnaes
    [HttpGet("{id:int}/cnaes")]
    public async Task<IActionResult> ListarCnaesEmpresa(int id)
    {
        var cnaes = await _context.Cnaes_empresas
            .Where(ce => ce.EmpresaId == id)
            .Join(_context.Cnae,
                ce => ce.CnaeNumero,
                c => c.Numero,
                (ce, c) => new { c.Numero, c.Descricao })
            .ToListAsync();

        return Ok(cnaes);
    }

    // POST: api/empresas/5/cnaes
    [HttpPost("{id:int}/cnaes")]
    public async Task<IActionResult> AssociarCnae(int id, [FromBody] AssociarCnaeDto dto)
    {
        var empresaExiste = await _context.Empresas.AnyAsync(e => e.Id == id);
        if (!empresaExiste)
            return NotFound(new { mensagem = "Empresa não encontrada." });

        var cnaeExiste = await _context.Cnae.AnyAsync(c => c.Numero == dto.CnaeNumero);
        if (!cnaeExiste)
            return NotFound(new { mensagem = "CNAE informado não existe na base." });

        var vinculoExiste = await _context.Cnaes_empresas
            .AnyAsync(ce => ce.EmpresaId == id && ce.CnaeNumero == dto.CnaeNumero);

        if (vinculoExiste)
            return BadRequest(new { mensagem = "Este CNAE já está associado à empresa." });

        var vinculo = new Cnaes_empresas
        {
            EmpresaId = id,
            CnaeNumero = dto.CnaeNumero
        };

        _context.Cnaes_empresas.Add(vinculo);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "CNAE associado com sucesso." });
    }

    // DELETE: api/empresas/5/cnaes/6201501
    [HttpDelete("{id:int}/cnaes/{cnaeNumero}")]
    public async Task<IActionResult> RemoverCnae(int id, string cnaeNumero)
    {
        var vinculo = await _context.Cnaes_empresas
            .FirstOrDefaultAsync(ce => ce.EmpresaId == id && ce.CnaeNumero == cnaeNumero);

        if (vinculo == null)
            return NotFound(new { mensagem = "Associação CNAE-Empresa não encontrada." });

        _context.Cnaes_empresas.Remove(vinculo);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public record AssociarCnaeDto(string CnaeNumero);