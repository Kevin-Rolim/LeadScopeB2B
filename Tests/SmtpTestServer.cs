using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

// Servidor SMTP TCP exclusivo dos testes: recebe mensagens reais do SmtpClient.
// Nunca é registrado na aplicação nem acessa caixas de e-mail externas.
internal sealed class SmtpTestServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<string> _messages = Channel.CreateUnbounded<string>();
    private readonly Task _loop;
    public int Porta { get; }
    public SmtpTestServer()
    {
        _listener.Start();
        Porta = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _loop = ExecutarAsync();
    }
    private async Task ExecutarAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                await writer.WriteLineAsync("220 localhost SMTP test");
                while (await reader.ReadLineAsync(_stop.Token) is string line)
                {
                    if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 End with <CRLF>.<CRLF>");
                        var message = new StringBuilder();
                        while (await reader.ReadLineAsync(_stop.Token) is string data && data != ".") message.Append(data.StartsWith("..") ? data[1..] : data).Append("\r\n");
                        await _messages.Writer.WriteAsync(message.ToString(), _stop.Token);
                        await writer.WriteLineAsync("250 Message received");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 Bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }
    public async Task<string> ReceberAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await _messages.Reader.ReadAsync(timeout.Token);
    }
    public bool TemMensagem => _messages.Reader.TryPeek(out _);
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _listener.Stop();
        await _loop;
        _stop.Dispose();
    }
}
