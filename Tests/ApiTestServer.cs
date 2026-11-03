using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

internal sealed record ApiTestRequest(string Method, string Path, Dictionary<string, string> Headers, string Body, CancellationToken Cancellation);
internal sealed record ApiTestResponse(int Status, string Body, Dictionary<string, string>? Headers = null);

// Servidor TCP real, HTTP/1.1 e TLS. Nenhuma chave/certificado de teste é usado pela aplicação.
internal sealed class ApiTestServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentBag<Task> requests = [];
    private readonly RSA rsa = RSA.Create(2048);
    private readonly Task loop;
    public X509Certificate2 Certificate { get; }
    public string Url { get; }
    public int Calls;
    public Func<ApiTestRequest, Task<ApiTestResponse>> Handler { get; set; } = r => Task.FromResult(new ApiTestResponse(200, """{"company":{"name":"Empresa simulada"}}"""));
    public ConcurrentQueue<ApiTestRequest> Received { get; } = new();
    public ApiTestServer(bool tls = false)
    {
        var cert = new CertificateRequest("CN=localhost", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        cert.CertificateExtensions.Add(san.Build());
        Certificate = cert.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        listener.Start();
        Url = (tls ? "https" : "http") + "://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        loop = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try { var client = await listener.AcceptTcpClientAsync(stop.Token); requests.Add(Process(client, tls)); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (stop.IsCancellationRequested) { break; }
            }
        });
    }
    private async Task Process(TcpClient client, bool tls)
    {
        using (client)
        {
            try
            {
                Stream stream = client.GetStream();
                if (tls) { var ssl = new SslStream(stream, false); await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = Certificate }, stop.Token); stream = ssl; }
                await using (stream)
                {
                    using var header = new MemoryStream(); var one = new byte[1];
                    while (header.Length < 32768)
                    {
                        if (await stream.ReadAsync(one, stop.Token) == 0) return;
                        header.WriteByte(one[0]);
                        var b = header.GetBuffer(); var n = (int)header.Length;
                        if (n >= 4 && b[n-4] == 13 && b[n-3] == 10 && b[n-2] == 13 && b[n-1] == 10) break;
                    }
                    var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                    var first = lines[0].Split(' ');
                    var headers = lines.Skip(1).Where(x => x.Contains(':')).ToDictionary(x => x[..x.IndexOf(':')], x => x[(x.IndexOf(':')+1)..].Trim(), StringComparer.OrdinalIgnoreCase);
                    var length = headers.TryGetValue("Content-Length", out var size) ? int.Parse(size) : 0;
                    if (length > 1024 * 1024) return;
                    var body = new byte[length]; var read = 0;
                    while (read < length) { var n = await stream.ReadAsync(body.AsMemory(read), stop.Token); if (n == 0) return; read += n; }
                    var req = new ApiTestRequest(first[0], first[1], headers, Encoding.UTF8.GetString(body), stop.Token);
                    Received.Enqueue(req); Interlocked.Increment(ref Calls);
                    var response = await Handler(req);
                    var bytes = Encoding.UTF8.GetBytes(response.Body);
                    var text = $"HTTP/1.1 {response.Status} Fixture\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n";
                    if (response.Headers is not null) foreach (var pair in response.Headers) text += pair.Key + ": " + pair.Value + "\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(text + "\r\n"), stop.Token); await stream.WriteAsync(bytes, stop.Token);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException or ObjectDisposedException) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); await loop;
        await Task.WhenAll(requests.ToArray()); Certificate.Dispose(); rsa.Dispose(); stop.Dispose();
    }
}
