using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDeckManagement.Contracts.Manager;

/// <summary>
/// Operações do protocolo local v1. Os nomes são estáveis e sensíveis a maiúsculas.
/// </summary>
public static class ManagerOperationsV1
{
    public const string Ping = "ping";
    public const string Inventory = "inventory.list";
    public const string Create = "service.create";
    public const string Update = "service.update";
    public const string Remove = "service.remove";
    public const string Start = "service.start";
    public const string Stop = "service.stop";
    public const string Restart = "service.restart";
    public const string Repair = "service.repair";
    public const string Logs = "service.logs.read";
}

public sealed record ManagerChallengeV1
{
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    [JsonRequired]
    public string Nonce { get; init; } = string.Empty;

    [JsonRequired]
    public string ServerProof { get; init; } = string.Empty;
}

public sealed record ManagerAuthenticationV1
{
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    [JsonRequired]
    public string Nonce { get; init; } = string.Empty;

    [JsonRequired]
    public string ClientProof { get; init; } = string.Empty;
}

public sealed record ManagerRequestV1
{
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    [JsonRequired]
    public string RequestId { get; init; } = string.Empty;

    [JsonRequired]
    public string Operation { get; init; } = string.Empty;

    [JsonRequired]
    public string ActorId { get; init; } = string.Empty;

    [JsonRequired]
    public string ActorRole { get; init; } = string.Empty;

    [JsonRequired]
    public JsonElement Payload { get; init; }
}

public sealed record ServiceLogsPayloadV1
{
    [JsonRequired]
    public string ServiceId { get; init; } = string.Empty;

    [JsonRequired]
    public long AfterSequence { get; init; }

    [JsonRequired]
    public int Limit { get; init; } = 200;
}

public sealed record ServiceLogEntryV1
{
    [JsonRequired]
    public DateTimeOffset Timestamp { get; init; }

    [JsonRequired]
    public long Sequence { get; init; }

    [JsonRequired]
    public string Stream { get; init; } = string.Empty;

    [JsonRequired]
    public string Message { get; init; } = string.Empty;
}

public sealed record ManagerResponseV1
{
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    [JsonRequired]
    public string RequestId { get; init; } = string.Empty;

    [JsonRequired]
    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public JsonElement? Data { get; init; }
}

public sealed record ServiceIdPayloadV1
{
    [JsonRequired]
    public string ServiceId { get; init; } = string.Empty;
}

public sealed record ManagedServiceSnapshotV1
{
    [JsonRequired]
    public string ServiceId { get; init; } = string.Empty;

    [JsonRequired]
    public string DisplayName { get; init; } = string.Empty;

    [JsonRequired]
    public string State { get; init; } = string.Empty;

    [JsonRequired]
    public string StartMode { get; init; } = string.Empty;

    [JsonRequired]
    public bool RegistrationMatches { get; init; }

    public int? ProcessId { get; init; }
}
