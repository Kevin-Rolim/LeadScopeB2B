using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LeadScopeB2B.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _repository;
    private readonly IPasswordHasher<Usuarios> _passwordHasher;

    public UsuarioService(
        IUsuarioRepository repository,
        IPasswordHasher<Usuarios> passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UsuarioListViewModel>> ListarAsync()
    {
        var usuarios = await _repository.ListarAsync();

        return usuarios.Select(u => new UsuarioListViewModel
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            PerfilAcesso = u.PerfisAcesso.Nome
        }).ToList();
    }

    public async Task<UsuarioDetailsViewModel?> ObterDetalhesAsync(int id)
    {
        var usuario = await _repository.ObterPorIdAsync(id);
        if (usuario is null)
        {
            return null;
        }

        return new UsuarioDetailsViewModel
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email,
            PerfilAcesso = usuario.PerfisAcesso.Nome
        };
    }

    public async Task PrepararFormularioAsync(UsuarioCreateViewModel model)
    {
        model.PerfisDisponiveis = await _repository.ListarPerfisAsync();
    }

    public async Task PrepararFormularioAsync(UsuarioEditViewModel model)
    {
        model.PerfisDisponiveis = await _repository.ListarPerfisAsync();
    }

    public async Task<UsuarioEditViewModel?> ObterParaEdicaoAsync(int id)
    {
        var usuario = await _repository.ObterPorIdAsync(id);
        if (usuario is null)
        {
            return null;
        }

        var model = new UsuarioEditViewModel
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email,
            PerfilAcessoId = usuario.PerfilAcessoId
        };
        await PrepararFormularioAsync(model);
        return model;
    }

    public async Task CadastrarAsync(UsuarioCreateViewModel model)
    {
        var email = NormalizarEmail(model.Email);
        await ValidarAsync(email, model.PerfilAcessoId);

        var usuario = new Usuarios
        {
            Nome = model.Nome.Trim(),
            Email = email,
            PerfilAcessoId = model.PerfilAcessoId
        };
        usuario.Senha = _passwordHasher.HashPassword(usuario, model.Senha);

        await _repository.AdicionarAsync(usuario);
    }

    public async Task EditarAsync(UsuarioEditViewModel model)
    {
        var usuario = await _repository.ObterPorIdAsync(model.Id)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        var email = NormalizarEmail(model.Email);
        await ValidarAsync(email, model.PerfilAcessoId, model.Id);

        usuario.Nome = model.Nome.Trim();
        usuario.Email = email;
        usuario.PerfilAcessoId = model.PerfilAcessoId;

        if (!string.IsNullOrWhiteSpace(model.NovaSenha))
        {
            usuario.Senha = _passwordHasher.HashPassword(usuario, model.NovaSenha);
        }

        await _repository.SalvarAlteracoesAsync();
    }

    private async Task ValidarAsync(string email, int perfilId, int? usuarioId = null)
    {
        if (await _repository.EmailExisteAsync(email, usuarioId))
        {
            throw new InvalidOperationException("Já existe um usuário cadastrado com este e-mail.");
        }

        if (!await _repository.PerfilExisteAsync(perfilId))
        {
            throw new InvalidOperationException("O perfil de acesso selecionado não existe.");
        }
    }

    private static string NormalizarEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
