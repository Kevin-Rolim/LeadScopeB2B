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
            Status = u.Status,
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
            Status = usuario.Status,
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
            PerfilAcessoId = usuario.PerfilAcessoId,
            Status = usuario.Status
        };
        await PrepararFormularioAsync(model);
        return model;
    }

    public async Task CadastrarAsync(UsuarioCreateViewModel model)
    {
        CadastroRules.Validar(model);
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

        CadastroRules.Validar(model);
        var email = NormalizarEmail(model.Email);
        await ValidarAsync(email, model.PerfilAcessoId, model.Id);

        usuario.Nome = model.Nome.Trim();
        usuario.Email = email;
        usuario.PerfilAcessoId = model.PerfilAcessoId;
        usuario.Status = CadastroRules.Estado(model.Status, "ATIVO", "BLOQUEADO", "DESATIVADO");

        if (!string.IsNullOrWhiteSpace(model.NovaSenha))
        {
            usuario.Senha = _passwordHasher.HashPassword(usuario, model.NovaSenha);
        }

        await _repository.AtualizarAsync(usuario);
    }

    public async Task AlterarBloqueioAsync(int id, bool bloquear)
    {
        var usuario = await _repository.ObterPorIdAsync(id) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (usuario.Status == "DESATIVADO") throw new ConflitoException("Reative a conta pela edição antes de alterar seu bloqueio.");
        usuario.Status = bloquear ? "BLOQUEADO" : "ATIVO";
        await _repository.AtualizarAsync(usuario);
    }

    public async Task ExcluirAsync(int id)
    {
        var usuario = await _repository.ObterPorIdAsync(id) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        usuario.Status = "DESATIVADO";
        await _repository.AtualizarAsync(usuario);
    }

    public async Task CriarAdministradorInicialAsync(string nome, string email, string senha)
    {
        var model = new UsuarioCreateViewModel { Nome = nome, Email = email, Senha = senha, PerfilAcessoId = 1 };
        CadastroRules.Validar(model);
        var usuario = new Usuarios { Nome = nome.Trim(), Email = NormalizarEmail(email) };
        usuario.Senha = _passwordHasher.HashPassword(usuario, senha);
        await _repository.CriarAdministradorInicialAsync(usuario);
    }

    private async Task ValidarAsync(string email, int perfilId, int? usuarioId = null)
    {
        if (await _repository.EmailExisteAsync(email, usuarioId))
        {
            throw new ConflitoException("Já existe um usuário cadastrado com este e-mail.");
        }

        if (!await _repository.PerfilExisteAsync(perfilId))
        {
            throw new ConflitoException("O perfil de acesso selecionado não existe.");
        }
    }

    private static string NormalizarEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
