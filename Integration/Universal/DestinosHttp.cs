using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Integration.Universal;

public sealed class DestinosHttp(IOptions<MotorIntegracoesOptions> options, Func<string, CancellationToken, Task<IPAddress[]>>? resolver = null)
{
    private Task<IPAddress[]> ResolverAsync(string host, CancellationToken ct) => resolver is null ? Dns.GetHostAddressesAsync(host, ct) : resolver(host, ct);
    public static readonly HttpRequestOptionsKey<string> Perfil = new("LeadScope.PerfilRede");
    public void ValidarUri(Uri uri, string perfil)
    {
        if (!uri.IsAbsoluteUri || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 || uri.AbsoluteUri.Length > 8192 || string.IsNullOrEmpty(uri.Host))
            throw new IntegracaoException("DESTINO_INVALIDO", "URL absoluta sem credenciais ou fragmento é obrigatória.");
        if (perfil == "publica")
        {
            if (uri.Scheme != "https") throw new IntegracaoException("HTTPS_OBRIGATORIO", "Integrações públicas exigem HTTPS.");
            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || !uri.Host.Contains('.') && !IPAddress.TryParse(uri.Host, out _))
                throw new IntegracaoException("SSRF", "Host local não permitido.");
            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && !EhPublico(ip))
                throw new IntegracaoException("SSRF", "Endereço local, reservado ou privado não permitido.");
        }
        else
        {
            var p = options.Value.RedesPrivadas.SingleOrDefault(p => p.Id == perfil)
                ?? throw new IntegracaoException("REDE_NAO_AUTORIZADA", "Perfil de rede não autorizado pelo operador do servidor.");
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && p.PermitirHttp)) throw new IntegracaoException("HTTPS_OBRIGATORIO", "O perfil não permite HTTP.");
            if (!p.Origens.Any(x => Uri.TryCreate(x, UriKind.Absolute, out var allowed) && allowed.GetLeftPart(UriPartial.Authority).Equals(uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)))
                throw new IntegracaoException("REDE_NAO_AUTORIZADA", "Origem não autorizada no perfil do servidor.");
        }
    }
    public bool EnderecoPermitido(IPAddress ip, string perfil)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (perfil == "publica") return EhPublico(ip);
        var p = options.Value.RedesPrivadas.SingleOrDefault(p => p.Id == perfil);
        return p is not null && p.Redes.Any(c => IPNetwork.TryParse(c, out var n) && n.Contains(ip));
    }
    public async Task ValidarAsync(Uri uri, string perfil, CancellationToken ct)
    {
        ValidarUri(uri, perfil);
        var ips = await ResolverAsync(uri.DnsSafeHost, ct);
        if (ips.Length == 0 || ips.Any(ip => !EnderecoPermitido(ip, perfil)))
            throw new IntegracaoException("SSRF", "DNS retornou endereço não autorizado.");
    }
    public async ValueTask<Stream> ConectarAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var request = context.InitialRequestMessage;
        var perfil = request.Options.TryGetValue(Perfil, out var configured) ? configured : "publica";
        ValidarUri(request.RequestUri!, perfil);
        // Resolve de novo no momento de conectar e usa diretamente o IP aprovado, impedindo DNS rebinding.
        var ips = await ResolverAsync(context.DnsEndPoint.Host, ct);
        if (ips.Length == 0 || ips.Any(ip => !EnderecoPermitido(ip, perfil))) throw new IntegracaoException("SSRF", "Conexão bloqueada pela política de rede.");
        foreach (var ip in ips)
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), ct); return new NetworkStream(socket, true); }
            catch (SocketException) { socket.Dispose(); }
            catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("Não foi possível conectar ao destino autorizado.");
    }
    public static bool EhPublico(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            string[] blocked = ["0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "172.16.0.0/12", "192.0.0.0/24", "192.0.2.0/24", "192.168.0.0/16", "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/3"];
            return !blocked.Any(c => IPNetwork.Parse(c).Contains(ip));
        }
        return ip.AddressFamily == AddressFamily.InterNetworkV6 && IPNetwork.Parse("2000::/3").Contains(ip)
            && !new[] { "2001:db8::/32", "2001::/32", "2001:2::/48", "2001:10::/28", "2001:20::/28", "2002::/16" }.Any(c => IPNetwork.Parse(c).Contains(ip));
    }
}
