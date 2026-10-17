using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Security;
using Microsoft.AspNetCore.Identity;

namespace LeadScopeB2B.Services;

public class ContaService(IUsuarioRepository usuarios, IPasswordHasher<Usuarios> hasher)
{
    // O mesmo trabalho de hashing é realizado para e-mails ausentes ou contas indisponíveis.
    private static readonly Usuarios Ausente = new();
    private static readonly string HashAusente = new PasswordHasher<Usuarios>().HashPassword(Ausente, Guid.NewGuid().ToString());

    private PasswordVerificationResult Verificar(Usuarios? usuario, string senha)
    {
        try { return hasher.VerifyHashedPassword(usuario ?? Ausente, usuario?.Senha ?? HashAusente, senha); }
        catch (FormatException)
        {
            _ = hasher.VerifyHashedPassword(Ausente, HashAusente, senha);
            return PasswordVerificationResult.Failed;
        }
    }

    public async Task<Usuarios?> AutenticarAsync(string email, string senha)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        var result = Verificar(usuario, senha);
        if (usuario is null || usuario.Status != "ATIVO" || Acesso.Papel(usuario) is null || result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var nova = hasher.HashPassword(usuario, senha);
            if (!await usuarios.AtualizarSenhaAsync(usuario.Id, usuario.Email, usuario.Senha, usuario.VersaoSessao, nova, "REHASH_SENHA")) return null;
            usuario = await usuarios.ObterPorIdAsync(usuario.Id);
        }
        return usuario;
    }

    public async Task CadastrarAsync(CadastroViewModel model)
    {
        CadastroRules.Validar(model);
        var usuario = new Usuarios { Nome = model.Nome.Trim(), Email = model.Email.Trim().ToLowerInvariant() };
        usuario.Senha = hasher.HashPassword(usuario, model.Senha);
        await usuarios.CadastrarPublicoAsync(usuario);
    }

    public async Task<PerfilViewModel> ObterPerfilAsync(int id)
    {
        var usuario = await ObterAtivoAsync(id);
        return new PerfilViewModel { Nome = usuario.Nome, Email = usuario.Email, PerfilAcesso = usuario.PerfisAcesso.Nome };
    }

    public async Task<Usuarios> AtualizarPerfilAsync(int id, PerfilViewModel model)
    {
        CadastroRules.Validar(model);
        var usuario = await ObterAtivoAsync(id);
        if (Verificar(usuario, model.SenhaAtual) == PasswordVerificationResult.Failed) throw new ValidationException("Senha atual incorreta.");
        return await usuarios.AtualizarPerfilAsync(id, model.Nome.Trim(), model.Email.Trim().ToLowerInvariant(), usuario.Senha, usuario.VersaoSessao);
    }

    public async Task AlterarSenhaAsync(int id, AlterarSenhaViewModel model)
    {
        CadastroRules.Validar(model);
        var usuario = await ObterAtivoAsync(id);
        if (Verificar(usuario, model.SenhaAtual) == PasswordVerificationResult.Failed) throw new ValidationException("Senha atual incorreta.");
        if (!await usuarios.AtualizarSenhaAsync(id, usuario.Email, usuario.Senha, usuario.VersaoSessao, hasher.HashPassword(usuario, model.NovaSenha), "ALTERACAO_SENHA"))
            throw new ConflitoException("Sua conta foi alterada. Entre novamente para continuar.");
    }

    private async Task<Usuarios> ObterAtivoAsync(int id)
    {
        var usuario = await usuarios.ObterPorIdAsync(id);
        return usuario is { Status: "ATIVO" } && Acesso.Papel(usuario) is not null ? usuario : throw new KeyNotFoundException("Conta indisponível.");
    }
}
