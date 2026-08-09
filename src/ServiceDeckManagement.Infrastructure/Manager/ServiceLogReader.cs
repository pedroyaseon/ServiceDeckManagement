using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Domain.Services;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Manager;

public sealed class ServiceLogReader(ProductPaths paths) : IServiceLogReader
{
    private const int MaximumLineCharacters = 65_536;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<IReadOnlyList<ServiceLogEntryV1>> ReadAsync(
        string serviceId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var canonical = ServiceId.Create(serviceId);
        if (afterSequence < 0 || limit is < 1 or > 500)
        {
            throw new InvalidDataException("Os limites da consulta de logs são inválidos.");
        }

        var directory = Path.Combine(paths.Logs, "services", canonical.Value);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        EnsureRegular(directory, isDirectory: true);
        var files = Directory.EnumerateFiles(directory, "service.*.log")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Concat([Path.Combine(directory, "service.log")])
            .Where(File.Exists)
            .ToArray();
        var result = new List<ServiceLogEntryV1>(limit);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRegular(file, isDirectory: false);
            await ReadFileAsync(file, afterSequence, limit, result, cancellationToken)
                .ConfigureAwait(false);
            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    private static async Task ReadFileAsync(
        string path,
        long afterSequence,
        int limit,
        List<ServiceLogEntryV1> result,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        while (result.Count < limit &&
               await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length is 0 or > MaximumLineCharacters)
            {
                throw new InvalidDataException("Uma entrada de log possui tamanho inválido.");
            }

            ServiceLogEntryV1? entry;
            try
            {
                entry = JsonSerializer.Deserialize<ServiceLogEntryV1>(line, JsonOptions);
            }
            catch (JsonException) when (stream.Position == stream.Length)
            {
                break;
            }

            if (entry is not null &&
                entry.Sequence > afterSequence &&
                entry.Message.Length <= 16_400 &&
                !entry.Message.Any(char.IsControl))
            {
                result.Add(entry);
            }
        }
    }

    private static void EnsureRegular(string path, bool isDirectory)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            isDirectory != ((attributes & FileAttributes.Directory) != 0))
        {
            throw new InvalidDataException("O caminho de logs não é regular.");
        }
    }
}
