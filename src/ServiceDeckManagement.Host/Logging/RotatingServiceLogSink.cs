using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.Host.Logging;

/// <summary>
/// Persiste JSON Lines em UTF-8 com rotação e retenção limitadas.
/// </summary>
public sealed class RotatingServiceLogSink : IServiceLogSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider timeProvider;
    private readonly string rootPath;
    private readonly string directoryPath;
    private readonly string activeFilePath;
    private readonly long maximumFileBytes;
    private readonly long maximumTotalBytes;
    private readonly int retainedFiles;
    private readonly bool enabled;
    private long sequence;
    private bool disposed;

    public RotatingServiceLogSink(
        ResolvedServiceDefinition service,
        IProductRootProvider rootProvider,
        TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        rootPath = Path.GetFullPath(rootProvider.RootPath);
        enabled = service.Definition.Logging.Enabled;
        maximumFileBytes = checked(
            service.Definition.Logging.MaximumFileSizeMb * 1_048_576L);
        maximumTotalBytes = checked(
            service.Definition.Logging.MaximumTotalSizeMb * 1_048_576L);
        retainedFiles = service.Definition.Logging.RetainedFiles;

        directoryPath = Path.Combine(
            rootProvider.RootPath,
            "logs",
            "services",
            service.Definition.Id);
        activeFilePath = Path.Combine(directoryPath, "service.log");
    }

    public async ValueTask WriteAsync(
        ServiceLogSource source,
        string message,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        if (!enabled)
        {
            return;
        }

        var sanitized = AnsiTextSanitizer.Sanitize(message);
        if (sanitized.Length == 0)
        {
            return;
        }

        const int maximumMessageCharacters = 16_384;
        if (sanitized.Length > maximumMessageCharacters)
        {
            sanitized = string.Concat(
                sanitized.AsSpan(0, maximumMessageCharacters),
                " [truncado]");
        }

        var entry = new ServiceLogEntry(
            timeProvider.GetUtcNow(),
            Interlocked.Increment(ref sequence),
            ToExternalName(source),
            sanitized);
        var payload = JsonSerializer.Serialize(entry, SerializerOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(payload);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectoryIsSafe();
            await RotateIfRequiredAsync(bytes.Length, cancellationToken)
                .ConfigureAwait(false);
            await using var streamWriter = new FileStream(
                activeFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 16_384,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await streamWriter.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await streamWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            gate.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void EnsureDirectoryIsSafe()
    {
        Directory.CreateDirectory(directoryPath);

        var current = new DirectoryInfo(directoryPath);
        while (current is not null && Directory.Exists(current.FullName))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("O diretório de logs não pode conter reparse points.");
            }

            current = current.Parent;
            if (current is not null && string.Equals(
                    current.FullName,
                    rootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("A raiz do produto não pode ser um reparse point.");
                }

                break;
            }
        }

        if (File.Exists(activeFilePath) &&
            (File.GetAttributes(activeFilePath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("O arquivo de log não pode ser um reparse point.");
        }
    }

    private async Task RotateIfRequiredAsync(
        int nextEntryBytes,
        CancellationToken cancellationToken)
    {
        var activeLength = File.Exists(activeFilePath)
            ? new FileInfo(activeFilePath).Length
            : 0;
        if (activeLength == 0 || activeLength + nextEntryBytes <= maximumFileBytes)
        {
            return;
        }

        var oldestPath = ArchivePath(retainedFiles);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var index = retainedFiles - 1; index >= 1; index--)
        {
            var source = ArchivePath(index);
            if (File.Exists(source))
            {
                File.Move(source, ArchivePath(index + 1));
            }
        }

        File.Move(activeFilePath, ArchivePath(1));
        await EnforceTotalLimitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task EnforceTotalLimitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archives = Directory
            .EnumerateFiles(directoryPath, "service.*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToList();

        var total = archives.Sum(file => file.Length);
        var archiveBudget = maximumTotalBytes - maximumFileBytes;
        foreach (var archive in archives)
        {
            if (total <= archiveBudget)
            {
                break;
            }

            total -= archive.Length;
            archive.Delete();
        }

        return Task.CompletedTask;
    }

    private string ArchivePath(int index) =>
        Path.Combine(directoryPath, $"service.{index:D3}.log");

    private static string ToExternalName(ServiceLogSource source) => source switch
    {
        ServiceLogSource.System => "system",
        ServiceLogSource.StandardOutput => "stdout",
        ServiceLogSource.StandardError => "stderr",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private sealed record ServiceLogEntry(
        DateTimeOffset Timestamp,
        long Sequence,
        string Stream,
        string Message);
}
