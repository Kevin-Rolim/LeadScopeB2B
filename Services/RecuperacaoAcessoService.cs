using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Integration;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Services;

public class RecuperacaoAcessoOptions
{
    public bool Habilitado { get; set; }
    public string UrlPublica { get; set; } = "";
}

public class RecuperacaoAcessoService(IUsuarioRepository usuarios, IPasswordHasher<Usuarios> hasher,
    RecuperacaoToken tokens, IRecuperacaoEmail email, SolicitacoesRecuperacao fila,
    IOptionsMonitor<RecuperacaoAcessoOptions> options, IWebHostEnvironment environment, AuditoriaService auditoria)
{
    public const string MensagemIndisponivel = "A recuperação por e-mail está indisponível. Entre em contato com o administrador.";
    public const string MensagemTokenInvalido = "O link é inválido ou expirou. Solicite uma nova recuperação.";

    private Uri? UrlPublica()
    {
        if (!Uri.TryCreate(options.CurrentValue.UrlPublica, UriKind.Absolute, out var uri)) return null;
        return (uri.Scheme == "https" || environment.IsDevelopment() && uri.Scheme == "http")
            && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : null;
    }
    public bool Disponivel => options.CurrentValue.Habilitado && email.Configurado && UrlPublica() is not null;

    public void Solicitar(RecuperarAcessoViewModel model)
    {
        CadastroRules.Validar(model);
        if (!Disponivel) throw new ConflitoException(MensagemIndisponivel);
        if (!fila.Enfileirar(model.Email.Trim().ToLowerInvariant())) throw new ConflitoException("A recuperação está ocupada no momento. Tente novamente mais tarde.");
    }

    // Executado apenas pelo worker. Sem conta ativa, não envia mensagem e não altera o usuário.
    public async Task EnviarAsync(string endereco, CancellationToken ct)
    {
        if (!Disponivel) throw new InvalidOperationException("Recuperação indisponível.");
        var usuario = await usuarios.ObterPorEmailAsync(endereco);
        if (usuario is null || usuario.Status != "ATIVO" || Acesso.Papel(usuario) is null) return;
        var link = UrlPublica()!.AbsoluteUri.TrimEnd('/') + "/Conta/RedefinirSenha?token=" + Uri.EscapeDataString(tokens.Criar(usuario));
        await email.EnviarAsync(usuario.Email, link, ct);
        await auditoria.RegistrarAsync(usuario.Id, "RECUPERACAO_EMAIL", "SMTP aceitou a mensagem de recuperação de acesso.");
    }

    private async Task<Usuarios?> ObterUsuarioAsync(string token)
    {
        var payload = tokens.Ler(token);
        if (payload is null) return null;
        var usuario = await usuarios.ObterPorIdAsync(payload.UsuarioId);
        return usuario is { Status: "ATIVO" } && Acesso.Papel(usuario) is not null
            && string.Equals(usuario.Email, payload.Email, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Acesso.Versao(usuario), payload.Versao, StringComparison.Ordinal) ? usuario : null;
    }
    public async Task<bool> TokenValidoAsync(string token) => await ObterUsuarioAsync(token) is not null;

    public async Task RedefinirAsync(RedefinirSenhaViewModel model)
    {
        CadastroRules.Validar(model);
        var usuario = await ObterUsuarioAsync(model.Token) ?? throw new ValidationException(MensagemTokenInvalido);
        // O UPDATE compara hash e versão: duas requisições não conseguem consumir o mesmo link.
        if (!await usuarios.AtualizarSenhaAsync(usuario.Id, usuario.Email, usuario.Senha, usuario.VersaoSessao,
            hasher.HashPassword(usuario, model.NovaSenha), "RECUPERACAO_SENHA")) throw new ValidationException(MensagemTokenInvalido);
    }
}
