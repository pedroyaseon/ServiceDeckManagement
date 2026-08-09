using System.Text;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Domain.Services;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Manager;

/// <summary>
/// Repositório UTF-8 com substituição atômica no mesmo volume.
/// </summary>
public sealed class AtomicServiceDefinitionRepository(
    ProductPaths paths,
    ServiceDefinitionValidator validator) : IServiceDefinitionRepository, IDisposable
{
    private const long MaximumDefinitionBytes = 1_048_576;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<ServiceDefinitionV1>> ListAsync(
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(paths.ServiceDefinitions))
            {
                return [];
            }

            EnsureRegularDirectory(paths.ServiceDefinitions);
            var result = new List<ServiceDefinitionV1>();
            foreach (var file in Directory.EnumerateFiles(
                         paths.ServiceDefinitions,
                         "*.json",
                         SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileId = Path.GetFileNameWithoutExtension(file);
                if (!ServiceId.TryCreate(fileId, out _))
                {
                    throw new InvalidDataException("Existe uma definição com nome inválido.");
                }

                var definition = await ReadFileAsync(file, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(fileId, definition.Id, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "O identificador interno não corresponde ao arquivo da definição.");
                }

                result.Add(definition);
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ServiceDefinitionV1?> FindAsync(
        string serviceId,
        CancellationToken cancellationToken)
    {
        var path = DefinitionPath(serviceId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var definition = await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(serviceId, definition.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "O identificador interno não corresponde ao arquivo da definição.");
            }

            return definition;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidDataException("A definição não passou pela validação estrita.");
        }

        var path = DefinitionPath(definition.Id);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDefinitionDirectory();
            if (File.Exists(path))
            {
                EnsureRegularFile(path);
            }

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                var payload = StrictUtf8.GetBytes(ServiceDefinitionJson.Serialize(definition) + "\n");
                await using (var stream = new FileStream(
                                 temporaryPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 16_384,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(string serviceId, CancellationToken cancellationToken)
    {
        var path = DefinitionPath(serviceId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            EnsureRegularFile(path);
            File.Delete(path);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ServiceDefinitionV1> ReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        EnsureRegularFile(path);
        var info = new FileInfo(path);
        if (info.Length > MaximumDefinitionBytes)
        {
            throw new InvalidDataException("A definição excede 1 MiB.");
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, StrictUtf8, true);
        string json;
        try
        {
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("A definição deve usar UTF-8 válido.", exception);
        }

        var definition = ServiceDefinitionJson.Deserialize(json);
        if (!validator.Validate(definition).IsValid)
        {
            throw new InvalidDataException("A definição persistida é inválida.");
        }

        return definition;
    }

    private string DefinitionPath(string serviceId)
    {
        var canonical = ServiceId.Create(serviceId);
        return Path.Combine(paths.ServiceDefinitions, $"{canonical.Value}.json");
    }

    private void EnsureDefinitionDirectory()
    {
        Directory.CreateDirectory(paths.Configuration);
        EnsureRegularDirectory(paths.Configuration);
        Directory.CreateDirectory(paths.ServiceDefinitions);
        EnsureRegularDirectory(paths.ServiceDefinitions);
    }

    private static void EnsureRegularDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("O diretório não pode ser um reparse point.");
        }
    }

    private static void EnsureRegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
        {
            throw new InvalidDataException("A definição deve ser um arquivo regular.");
        }
    }

    public void Dispose() => gate.Dispose();
}
