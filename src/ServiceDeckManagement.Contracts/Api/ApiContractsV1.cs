using System.Text.Json.Serialization;
using ServiceDeckManagement.Contracts.Manager;

namespace ServiceDeckManagement.Contracts.Api;

public static class ApiRolesV1
{
    public const string Viewer = "viewer";
    public const string Operator = "operator";
    public const string Administrator = "administrator";
}

public sealed record BootstrapRequestV1
{
    [JsonRequired]
    public string Code { get; init; } = string.Empty;

    [JsonRequired]
    public string Username { get; init; } = string.Empty;

    [JsonRequired]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginRequestV1
{
    [JsonRequired]
    public string Username { get; init; } = string.Empty;

    [JsonRequired]
    public string Password { get; init; } = string.Empty;
}

public sealed record SessionResponseV1
{
    [JsonRequired]
    public string AccessToken { get; init; } = string.Empty;

    [JsonRequired]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonRequired]
    public UserSummaryV1 User { get; init; } = new();
}

public sealed record UserSummaryV1
{
    [JsonRequired]
    public string Id { get; init; } = string.Empty;

    [JsonRequired]
    public string Username { get; init; } = string.Empty;

    [JsonRequired]
    public string Role { get; init; } = string.Empty;
}

public sealed record BootstrapStatusV1
{
    [JsonRequired]
    public bool Required { get; init; }
}

public sealed record SystemHealthV1
{
    [JsonRequired]
    public string Api { get; init; } = string.Empty;

    [JsonRequired]
    public string Manager { get; init; } = string.Empty;
}

public sealed record VersionResponseV1
{
    [JsonRequired]
    public string Version { get; init; } = string.Empty;

    [JsonRequired]
    public string ApiVersion { get; init; } = "v1";
}

public sealed record ServiceSnapshotEnvelopeV1
{
    [JsonRequired]
    public long Sequence { get; init; }

    [JsonRequired]
    public DateTimeOffset GeneratedAt { get; init; }

    [JsonRequired]
    public IReadOnlyList<ManagedServiceSnapshotV1> Services { get; init; } = [];
}

public sealed record AuditEntryV1
{
    [JsonRequired]
    public long Id { get; init; }

    [JsonRequired]
    public DateTimeOffset Timestamp { get; init; }

    [JsonRequired]
    public string ActorId { get; init; } = string.Empty;

    [JsonRequired]
    public string Action { get; init; } = string.Empty;

    [JsonRequired]
    public string Target { get; init; } = string.Empty;

    [JsonRequired]
    public bool Success { get; init; }
}
