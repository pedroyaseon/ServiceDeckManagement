using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.Host;

/// <summary>
/// Carrega uma definição imutável por execução e falha de modo fechado.
/// </summary>
public sealed class ServiceDefinitionLoader(
    IProductRootProvider rootProvider,
    IPortablePathResolver pathResolver,
    ServiceDefinitionValidator validator)
{
    private const long MaximumDefinitionBytes = 1_048_576;

    public async Task<ResolvedServiceDefinition> LoadAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        if (!ServiceId.TryCreate(serviceId, out var canonicalId))
        {
            throw new InvalidDataException("O identificador do serviço é inválido.");
        }

        var relativeDefinitionPath =
            $"config/services/{canonicalId.Value}.json";
        var definitionPath = ResolveRequiredPath(relativeDefinitionPath);
        EnsureRegularFile(definitionPath, "A definição do serviço não foi encontrada.");

        var fileInfo = new FileInfo(definitionPath);
        if (fileInfo.Length > MaximumDefinitionBytes)
        {
            throw new InvalidDataException("A definição do serviço excede 1 MiB.");
        }

        string json;
        try
        {
            await using var stream = new FileStream(
                definitionPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16_384,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(
                stream,
                new System.Text.UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "A definição do serviço deve usar UTF-8 válido.",
                exception);
        }

        ServiceDefinitionV1 definition;
        try
        {
            definition = ServiceDefinitionJson.Deserialize(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidDataException(
                "A definição do serviço não corresponde ao contrato v1.",
                exception);
        }

        if (!string.Equals(definition.Id, canonicalId.Value, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "O identificador interno não corresponde ao nome da definição.");
        }

        var validation = validator.Validate(definition);
        if (!validation.IsValid)
        {
            var fields = string.Join(", ", validation.Errors.Select(item => item.Field));
            throw new InvalidDataException(
                $"A definição do serviço é inválida nos campos: {fields}.");
        }

        if (definition.SecretReferences.Count > 0)
        {
            throw new InvalidDataException(
                "Referências de segredo exigem o Manager e ainda não podem ser resolvidas pelo Host.");
        }

        var executablePath = ResolveRequiredPath(definition.Executable);
        EnsureRegularFile(executablePath, "O executável gerenciado não foi encontrado.");

        var workingDirectoryPath = ResolveRequiredPath(definition.WorkingDirectory);
        EnsureRegularDirectory(
            workingDirectoryPath,
            "O diretório de trabalho não foi encontrado.");

        _ = rootProvider.RootPath;
        return new(definition, executablePath, workingDirectoryPath);
    }

    private string ResolveRequiredPath(string relativePath)
    {
        var result = pathResolver.Resolve(relativePath);
        if (!result.IsValid || string.IsNullOrEmpty(result.FullPath))
        {
            throw new InvalidDataException(
                result.ErrorMessage ?? "O caminho relativo é inválido.");
        }

        return result.FullPath;
    }

    private static void EnsureRegularFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(message);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidDataException("O arquivo não pode ser um reparse point.");
        }
    }

    private static void EnsureRegularDirectory(string path, string message)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(message);
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("O diretório não pode ser um reparse point.");
        }
    }
}
