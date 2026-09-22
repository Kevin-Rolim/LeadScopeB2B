using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/* Ajustar para o namespace do DbContext futuramente
using LeadScopeB2B.Data; 
using LeadScopeB2B.Models; */

namespace LeadScopeB2B.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly LeadScopeDbContext _context;

    public UsuariosController(LeadScopeDbContext context)
    {
        _context = context;
    }

    // GET: api/usuarios
    [HttpGet]
    public async Task<IActionResult> ListarUsuarios()
    {
        var usuarios = await _context.Usuarios
            .Select(u => new 
            {
                u.Id,
                u.Nome,
                u.Email,
                u.PerfilAcessoId
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    // GET: api/usuarios/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var usuario = await _context.Usuarios
            .Where(u => u.Id == id)
            .Select(u => new 
            {
                u.Id,
                u.Nome,
                u.Email,
                u.PerfilAcessoId
            })
            .FirstOrDefaultAsync();

        if (usuario == null)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        return Ok(usuario);
    }

    // POST: api/usuarios
    [HttpPost]
    public async Task<IActionResult> CadastrarUsuario([FromBody] CadastrarUsuarioDto dto)
    {
        var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == dto.Email);
        if (emailExiste)
            return BadRequest(new { mensagem = "Já existe um usuário cadastrado com este e-mail." });

        var perfilExiste = await _context.Perfis_acesso.AnyAsync(p => p.Id == dto.PerfilAcessoId);
        if (!perfilExiste)
            return BadRequest(new { mensagem = "Perfil de acesso informado não existe." });

        var usuario = new Usuarios
        {
            Nome = dto.Nome,
            Email = dto.Email,
            Senha = dto.Senha, // Em produção, armazene sempre o hash seguro da senha
            PerfilAcessoId = dto.PerfilAcessoId
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, new 
        {
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.PerfilAcessoId
        });
    }

    // PUT: api/usuarios/5/senha
    [HttpPut("{id:int}/senha")]
    public async Task<IActionResult> AlterarSenha(int id, [FromBody] AlterarSenhaDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        usuario.Senha = dto.NovaSenha;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PUT: api/usuarios/5/perfil
    [HttpPut("{id:int}/perfil")]
    public async Task<IActionResult> AtribuirPerfil(int id, [FromBody] AtribuirPerfilDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        var perfilExiste = await _context.Perfis_acesso.AnyAsync(p => p.Id == dto.NovoPerfilAcessoId);
        if (!perfilExiste)
            return BadRequest(new { mensagem = "Perfil de acesso não encontrado." });

        usuario.PerfilAcessoId = dto.NovoPerfilAcessoId;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/usuarios/perfis
    [HttpGet("perfis")]
    public async Task<IActionResult> ListarPerfis()
    {
        var perfis = await _context.Perfis_acesso.ToListAsync();
        return Ok(perfis);
    }

    // POST: api/usuarios/perfis
    [HttpPost("perfis")]
    public async Task<IActionResult> CriarPerfil([FromBody] Perfis_acesso perfil)
    {
        var conjuntoExiste = await _context.Conjuntos_permissao.AnyAsync(c => c.Id == perfil.ConjuntoPermissoesId);
        if (!conjuntoExiste)
            return BadRequest(new { mensagem = "Conjunto de permissões informado não existe." });

        _context.Perfis_acesso.Add(perfil);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ListarPerfis), new { id = perfil.Id }, perfil);
    }

    // GET: api/usuarios/permissoes
    [HttpGet("permissoes")]
    public async Task<IActionResult> ListarConjuntosPermissoes()
    {
        var permissoes = await _context.Conjuntos_permissao.ToListAsync();
        return Ok(permissoes);
    }

    // POST: api/usuarios/permissoes
    [HttpPost("permissoes")]
    public async Task<IActionResult> CriarConjuntoPermissao([FromBody] Conjuntos_permissao conjunto)
    {
        _context.Conjuntos_permissao.Add(conjunto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ListarConjuntosPermissoes), new { id = conjunto.Id }, conjunto);
    }
}

public record CadastrarUsuarioDto(string Nome, string Email, string Senha, int PerfilAcessoId);
public record AlterarSenhaDto(string NovaSenha);
public record AtribuirPerfilDto(int NovoPerfilAcessoId);