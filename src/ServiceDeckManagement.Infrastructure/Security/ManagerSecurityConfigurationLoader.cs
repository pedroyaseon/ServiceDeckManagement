using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Principal;
using System.Runtime.Versioning;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class ManagerSecurityConfigurationLoader(ProductPaths paths)
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

    public ManagerSecurityOptions Load()
    {
        var path = Path.Combine(paths.Configuration, "manager-security.json");
        if (!File.Exists(path))
        {
            return ManagerSecurityOptions.LocalAdministratorsOnly;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A configuração de segurança deve ser um arquivo regular.");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > 16_384)
        {
            throw new InvalidDataException("A configuração de segurança possui tamanho inválido.");
        }

        ManagerSecurityFile file;
        try
        {
            var json = new UTF8Encoding(false, true).GetString(bytes);
            file = JsonSerializer.Deserialize<ManagerSecurityFile>(json, JsonOptions) ??
                throw new InvalidDataException("A configuração de segurança está vazia.");
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException(
                "A configuração de segurança deve usar o contrato v1 e UTF-8 válido.",
                exception);
        }

        if (file.SchemaVersion != 1)
        {
            throw new InvalidDataException("A versão da configuração de segurança não é suportada.");
        }

        if (string.IsNullOrWhiteSpace(file.ApiClientSid))
        {
            return ManagerSecurityOptions.LocalAdministratorsOnly;
        }

        try
        {
            var sid = new SecurityIdentifier(file.ApiClientSid);
            if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
            {
                throw new InvalidDataException("O SID da API deve representar uma identidade dedicada.");
            }

            return new(sid.Value);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("O SID configurado para a API é inválido.", exception);
        }
    }

    private sealed record ManagerSecurityFile
    {
        [JsonRequired]
        public int SchemaVersion { get; init; }

        public string? ApiClientSid { get; init; }
    }
}
