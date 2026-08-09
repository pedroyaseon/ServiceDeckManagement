using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Manager;

/// <summary>
/// Log append-only com encadeamento SHA-256 para detectar alteração acidental.
/// </summary>
public sealed class HashChainedAuditLog(ProductPaths paths) : IAuditLog, IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? lastHash;
    private bool initialized;

    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Validate(auditEvent);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(paths.ManagerData);
            EnsureRegularDirectory(paths.ManagerData);
            if (!initialized)
            {
                lastHash = await VerifyAndGetLastHashAsync(cancellationToken).ConfigureAwait(false);
                initialized = true;
            }

            var previousHash = lastHash ?? new string('0', 64);
            var unsigned = new UnsignedAuditEntry(previousHash, auditEvent);
            var canonical = JsonSerializer.SerializeToUtf8Bytes(unsigned, Options);
            var hash = Convert.ToHexStringLower(SHA256.HashData(canonical));
            var line = JsonSerializer.SerializeToUtf8Bytes(
                new StoredAuditEntry(previousHash, hash, auditEvent), Options);

            await using var stream = new FileStream(
                paths.ManagerAudit,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                16_384,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
            lastHash = hash;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string?> VerifyAndGetLastHashAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ManagerAudit))
        {
            return null;
        }

        EnsureRegularFile(paths.ManagerAudit);
        var expectedPrevious = new string('0', 64);
        await using var stream = new FileStream(
            paths.ManagerAudit,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, StrictUtf8, true);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException("O log de auditoria contém uma linha vazia.");
            }

            var entry = JsonSerializer.Deserialize<StoredAuditEntry>(line, Options) ??
                throw new InvalidDataException("O log de auditoria contém uma entrada vazia.");
            if (!string.Equals(entry.PreviousHash, expectedPrevious, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A cadeia do log de auditoria foi alterada.");
            }

            var canonical = JsonSerializer.SerializeToUtf8Bytes(
                new UnsignedAuditEntry(entry.PreviousHash, entry.Event), Options);
            var calculated = Convert.ToHexStringLower(SHA256.HashData(canonical));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(calculated),
                    Encoding.ASCII.GetBytes(entry.Hash)))
            {
                throw new InvalidDataException("A integridade do log de auditoria falhou.");
            }

            expectedPrevious = entry.Hash;
        }

        return expectedPrevious == new string('0', 64) ? null : expectedPrevious;
    }

    private static void Validate(AuditEvent value)
    {
        ValidateText(value.Actor, 256, nameof(value.Actor));
        ValidateText(value.Operation, 128, nameof(value.Operation));
        ValidateText(value.ResultCode, 128, nameof(value.ResultCode));
        ValidateText(value.CorrelationId, 128, nameof(value.CorrelationId));
        if (value.ServiceId is not null)
        {
            ValidateText(value.ServiceId, 63, nameof(value.ServiceId));
        }
    }

    private static void ValidateText(string value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(char.IsControl) ||
            !value.IsNormalized(NormalizationForm.FormC))
        {
            throw new InvalidDataException($"O campo de auditoria {field} é inválido.");
        }
    }

    private static void EnsureRegularDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("O diretório de auditoria não pode ser reparse point.");
        }
    }

    private static void EnsureRegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
        {
            throw new InvalidDataException("O log de auditoria deve ser um arquivo regular.");
        }
    }

    private sealed record UnsignedAuditEntry(string PreviousHash, AuditEvent Event);

    private sealed record StoredAuditEntry(string PreviousHash, string Hash, AuditEvent Event);

    public void Dispose() => gate.Dispose();
}
