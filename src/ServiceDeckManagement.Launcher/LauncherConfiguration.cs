using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Launcher;

public sealed record LauncherOptions(Uri ApiBaseUri);

public sealed class LauncherConfiguration(ProductPaths paths)
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

    public LauncherOptions Load()
    {
        var path = Path.Combine(paths.Configuration, "launcher.json");
        if (!File.Exists(path))
        {
            return new(new Uri("http://127.0.0.1:5180/", UriKind.Absolute));
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A configuração do Launcher deve ser um arquivo regular.");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > 16_384)
        {
            throw new InvalidDataException("A configuração do Launcher possui tamanho inválido.");
        }

        LauncherConfigurationFile file;
        try
        {
            file = JsonSerializer.Deserialize<LauncherConfigurationFile>(
                new UTF8Encoding(false, true).GetString(bytes), JsonOptions) ??
                throw new InvalidDataException("A configuração do Launcher está vazia.");
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("A configuração do Launcher deve usar o contrato v1 e UTF-8 válido.", exception);
        }

        if (file.SchemaVersion != 1 || !Uri.TryCreate(file.ApiBaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || !System.Net.IPAddress.TryParse(uri.Host, out var address) ||
            !System.Net.IPAddress.IsLoopback(address) || uri.Port is < 1024 or > 65535 ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidDataException("A URL da API não é compatível com o limite local desta versão.");
        }

        return new(uri);
    }

    private sealed record LauncherConfigurationFile
    {
        [JsonRequired]
        public int SchemaVersion { get; init; }

        [JsonRequired]
        public string ApiBaseUrl { get; init; } = string.Empty;
    }
}
