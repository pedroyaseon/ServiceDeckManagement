using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Api;

public sealed record ApiOptions(int Port);

public sealed class ApiConfiguration(ProductPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public ApiOptions Load()
    {
        var path = Path.Combine(paths.Configuration, "api.json");
        if (!File.Exists(path))
        {
            return new(5180);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A configuração da API deve ser um arquivo regular.");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > 16_384)
        {
            throw new InvalidDataException("A configuração da API possui tamanho inválido.");
        }

        ApiConfigurationFile file;
        try
        {
            file = JsonSerializer.Deserialize<ApiConfigurationFile>(
                new UTF8Encoding(false, true).GetString(bytes), JsonOptions) ??
                throw new InvalidDataException("A configuração da API está vazia.");
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("A configuração da API deve usar o contrato v1 e UTF-8 válido.", exception);
        }

        if (file.SchemaVersion != 1 || file.Port is < 1024 or > 65535 ||
            file.RemoteAccess || !string.Equals(file.BindAddress, "127.0.0.1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A configuração solicita uma opção não suportada com segurança nesta versão.");
        }

        return new(file.Port);
    }

    private sealed record ApiConfigurationFile
    {
        [JsonRequired]
        public int SchemaVersion { get; init; }

        [JsonRequired]
        public string BindAddress { get; init; } = string.Empty;

        [JsonRequired]
        public int Port { get; init; }

        [JsonRequired]
        public bool RemoteAccess { get; init; }
    }
}
