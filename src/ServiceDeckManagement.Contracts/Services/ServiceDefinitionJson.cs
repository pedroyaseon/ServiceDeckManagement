using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDeckManagement.Contracts.Services;

/// <summary>
/// Serialização estrita do contrato de configuração v1.
/// </summary>
public static class ServiceDefinitionJson
{
    private static JsonSerializerOptions Options { get; } = CreateOptions();

    public static ServiceDefinitionV1 Deserialize(string json) =>
        JsonSerializer.Deserialize<ServiceDefinitionV1>(json, Options) ??
        throw new JsonException("A definição do serviço está vazia.");

    public static string Serialize(ServiceDefinitionV1 definition) =>
        JsonSerializer.Serialize(definition, Options);

    private static JsonSerializerOptions CreateOptions() =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false,
            AllowDuplicateProperties = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true
        };
}
