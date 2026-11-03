using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeadScopeB2B.Integration.Universal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace LeadScopeB2B.Repositories;

// Uma instância/processo por diretório. O lock de arquivo também impede segundo processo de gravar.
public sealed class IntegracaoArquivoRepository : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 48, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly string diretorio;
    private readonly IDataProtectionProvider protection;
    private FileStream? processLock;
    public IntegracaoArquivoRepository(IOptions<MotorIntegracoesOptions> options, IDataProtectionProvider protection)
    {
        diretorio = Path.GetFullPath(options.Value.Diretorio);
        this.protection = protection;
    }
    private void Preparar()
    {
        if (processLock is not null) return;
        Directory.CreateDirectory(diretorio);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(diretorio, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        processLock = new FileStream(Path.Combine(diretorio, ".process.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private EstadoIntegracoes Ler()
    {
        var path = Path.Combine(diretorio, "configuracao.json");
        if (!File.Exists(path))
        {
            if (File.Exists(path + ".bak") || Directory.GetFiles(diretorio, "segredo-*.bin").Length > 0) throw new IntegracaoException("ARQUIVO_INVALIDO", "Configuração ausente, mas existe backup. Restaure o armazenamento antes de continuar.");
            return new();
        }
        try
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new JsonException();
            var state = JsonSerializer.Deserialize<EstadoIntegracoes>(File.ReadAllText(path), Json) ?? throw new JsonException();
            if (state.Versao != 1 || state.Integracoes is null || state.Consumo is null || state.Historico is null || state.ContratosAnteriores is null || state.Integracoes.Count > 20 || state.Historico.Count > 2000 || state.Integracoes.Any(a => a is null || a.Contrato is null) || state.Integracoes.Select(x => x.Id).Distinct().Count() != state.Integracoes.Count) throw new JsonException();
            return state;
        }
        catch (JsonException) { throw new IntegracaoException("ARQUIVO_INVALIDO", "Armazenamento de integrações inválido. Nenhum arquivo foi substituído; restaure um backup."); }
    }
    private static async Task AtomicoAsync(string path, byte[] bytes, CancellationToken ct)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await file.WriteAsync(bytes, ct);
                file.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public async Task<T> AlterarAsync<T>(Func<EstadoIntegracoes, T> alteracao, CancellationToken ct = default)
    {
        await mutex.WaitAsync(ct);
        try
        {
            Preparar();
            var state = Ler();
            var result = alteracao(state);
            var path = Path.Combine(diretorio, "configuracao.json");
            if (File.Exists(path)) await AtomicoAsync(path + ".bak", await File.ReadAllBytesAsync(path, ct), ct);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Json);
            if (bytes.Length > 32 * 1024 * 1024) throw new IntegracaoException("ARMAZENAMENTO_LIMITE", "Estado operacional excede 32 MiB. Exporte e revise a retenção antes de continuar.");
            await AtomicoAsync(path, bytes, ct);
            return result;
        }
        finally { mutex.Release(); }
    }
    public async Task<EstadoIntegracoes> LerAsync(CancellationToken ct = default)
    {
        await mutex.WaitAsync(ct);
        try { Preparar(); return Ler(); }
        finally { mutex.Release(); }
    }
    private string ArquivoSegredo(Guid id, string authId)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(authId, "^[a-zA-Z0-9_.-]{1,60}$")) throw new IntegracaoException("AUTENTICACAO", "Identificador de credencial inválido.");
        return Path.Combine(diretorio, $"segredo-{id:N}-{authId}.bin");
    }
    public async Task<SegredoApi> LerSegredoAsync(Guid id, string authId, CancellationToken ct = default)
    {
        await mutex.WaitAsync(ct);
        try
        {
            Preparar();
            var path = ArquivoSegredo(id, authId);
            if (!File.Exists(path)) return new();
            try
            {
                if (new FileInfo(path).Length > 256 * 1024) throw new JsonException();
                var bytes = protection.CreateProtector("Integracoes.Segredos.v1", id.ToString("N"), authId).Unprotect(await File.ReadAllBytesAsync(path, ct));
                return JsonSerializer.Deserialize<SegredoApi>(bytes, Json) ?? throw new JsonException();
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
            { throw new IntegracaoException("SEGREDO_INVALIDO", "Não foi possível abrir a credencial. Verifique as chaves de proteção ou recadastre o segredo."); }
        }
        finally { mutex.Release(); }
    }
    public async Task SalvarSegredoAsync(Guid id, string authId, SegredoApi segredo, CancellationToken ct = default)
    {
        await mutex.WaitAsync(ct);
        try
        {
            Preparar();
            var path = ArquivoSegredo(id, authId);
            var bytes = protection.CreateProtector("Integracoes.Segredos.v1", id.ToString("N"), authId).Protect(JsonSerializer.SerializeToUtf8Bytes(segredo, Json));
            if (File.Exists(path)) await AtomicoAsync(path + ".bak", await File.ReadAllBytesAsync(path, ct), ct);
            await AtomicoAsync(path, bytes, ct);
        }
        finally { mutex.Release(); }
    }
    public async Task<byte[]> BackupAsync(CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        try
        {
            Preparar(); Ler(); // Não exporta silenciosamente um estado corrompido.
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                foreach (var path in Directory.GetFiles(diretorio).Where(p => p.EndsWith(".json") || p.EndsWith(".bin")))
                {
                    var entry = zip.CreateEntry(Path.GetFileName(path));
                    await using var output = entry.Open();
                    var bytes = await File.ReadAllBytesAsync(path, ct);
                    await output.WriteAsync(bytes, ct);
                }
            // Backup contém segredos ainda criptografados. As chaves de Data Protection têm backup separado pelo operador.
            return buffer.ToArray();
        }
        finally { mutex.Release(); }
    }
    public void Dispose() { processLock?.Dispose(); mutex.Dispose(); }
}
