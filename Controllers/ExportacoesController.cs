using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportacoesController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public ExportacoesController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/exportacoes
    [HttpGet]
    public async Task<IActionResult> ListarHistorico()
    {
        var historico = await _context.Exportacoes
            .OrderByDescending(e => e.DataHora)
            .ToListAsync();

        return Ok(historico);
    }

    // GET: api/exportacoes/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterDetalhesExportacao(int id)
    {
        var exportacao = await _context.Exportacoes.FindAsync(id);
        if (exportacao == null)
            return NotFound(new { mensagem = "Registro de exportação não encontrado." });

        var itens = await _context.Exportacoes_pessoas_empresas
            .Where(epe => epe.ExportacaoId == id)
            .ToListAsync();

        return Ok(new { Exportacao = exportacao, Itens = itens });
    }

    // POST: api/exportacoes
    [HttpPost]
    public async Task<IActionResult> RegistrarExportacao([FromBody] RegistrarExportacaoDto dto)
    {
        var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == dto.UsuarioId);
        if (!usuarioExiste)
            return BadRequest(new { mensagem = "Usuário responsável não encontrado." });

        if (dto.Leads == null || !dto.Leads.Any())
            return BadRequest(new { mensagem = "Nenhum par de lead/empresa foi informado para exportação." });

        // Cria o registro da exportação
        var exportacao = new Exportacoes
        {
            UsuarioId = dto.UsuarioId,
            QntdItens = dto.Leads.Count,
            DataHora = DateTime.UtcNow
        };

        _context.Exportacoes.Add(exportacao);
        await _context.SaveChangesAsync(); // Gera o Id da exportação

        // Adiciona os pares Pessoa-Empresa associados
        var itensExportados = dto.Leads.Select(item => new Exportacoes_pessoas_empresas
        {
            ExportacaoId = exportacao.Id,
            PessoaId = item.PessoaId,
            EmpresaId = item.EmpresaId
        }).ToList();

        _context.Exportacoes_pessoas_empresas.AddRange(itensExportados);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterDetalhesExportacao), new { id = exportacao.Id }, exportacao);
    }
}

public record ParLeadEmpresaDto(int PessoaId, int EmpresaId);
public record RegistrarExportacaoDto(int UsuarioId, List<ParLeadEmpresaDto> Leads);