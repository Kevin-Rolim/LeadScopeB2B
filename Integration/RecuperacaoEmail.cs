using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Integration;

public class EmailSmtpOptions
{
    public string Host { get; set; } = "";
    public int Porta { get; set; } = 587;
    public string Remetente { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string Senha { get; set; } = "";
    public bool UsarTls { get; set; } = true;
}

public interface IRecuperacaoEmail
{
    bool Configurado { get; }
    Task EnviarAsync(string email, string link, CancellationToken cancellationToken);
}

public class SmtpRecuperacaoEmail(IOptionsMonitor<EmailSmtpOptions> options, IWebHostEnvironment environment) : IRecuperacaoEmail
{
    public bool Configurado
    {
        get
        {
            var o = options.CurrentValue;
            return !string.IsNullOrWhiteSpace(o.Host) && o.Porta is > 0 and <= 65535
                && MailAddress.TryCreate(o.Remetente, out var remetente) && remetente.Address == o.Remetente
                && (environment.IsDevelopment() || o.UsarTls)
                && string.IsNullOrEmpty(o.Usuario) == string.IsNullOrEmpty(o.Senha);
        }
    }

    public async Task EnviarAsync(string email, string link, CancellationToken cancellationToken)
    {
        if (!Configurado) throw new InvalidOperationException("SMTP não configurado para recuperação de acesso.");
        var o = options.CurrentValue;
        using var client = new SmtpClient(o.Host, o.Porta)
        {
            EnableSsl = o.UsarTls,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrEmpty(o.Usuario)) client.Credentials = new NetworkCredential(o.Usuario, o.Senha);
        using var message = new MailMessage(o.Remetente, email)
        {
            Subject = "Recuperar acesso ao LeadScopeB2B",
            Body = $"Use o link abaixo para definir uma nova senha. Ele expira em uma hora e só pode ser usado uma vez.\r\n\r\n{link}\r\n\r\nSe você não solicitou a recuperação, ignore esta mensagem.",
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await client.SendMailAsync(message, timeout.Token);
    }
}
