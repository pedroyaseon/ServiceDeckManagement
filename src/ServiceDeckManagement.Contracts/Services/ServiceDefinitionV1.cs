using System.Text.Json.Serialization;

namespace ServiceDeckManagement.Contracts.Services;

/// <summary>
/// Contrato persistido de uma aplicação gerenciada pela versão 1.
/// </summary>
public sealed record ServiceDefinitionV1
{
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonRequired]
    public string Id { get; init; } = string.Empty;

    [JsonRequired]
    public string DisplayName { get; init; } = string.Empty;

    [JsonRequired]
    public string Executable { get; init; } = string.Empty;

    [JsonRequired]
    public string WorkingDirectory { get; init; } = string.Empty;

    [JsonRequired]
    public string[] Arguments { get; init; } = [];

    [JsonRequired]
    public Dictionary<string, string> Environment { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonRequired]
    public Dictionary<string, string> SecretReferences { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonRequired]
    public string StartMode { get; init; } = "manual";

    [JsonRequired]
    public RestartPolicyV1 RestartPolicy { get; init; } = new();

    [JsonRequired]
    public StopPolicyV1 StopPolicy { get; init; } = new();

    [JsonRequired]
    public LoggingPolicyV1 Logging { get; init; } = new();

    [JsonRequired]
    public HealthCheckV1 HealthCheck { get; init; } = new();
}

public sealed record RestartPolicyV1
{
    [JsonRequired]
    public bool Enabled { get; init; }

    [JsonRequired]
    public int MaximumAttempts { get; init; } = 5;

    [JsonRequired]
    public int DelaySeconds { get; init; } = 10;

    [JsonRequired]
    public int MaximumDelaySeconds { get; init; } = 120;

    [JsonRequired]
    public int ResetAfterMinutes { get; init; } = 15;
}

public sealed record StopPolicyV1
{
    [JsonRequired]
    public int GracefulTimeoutSeconds { get; init; } = 20;

    [JsonRequired]
    public bool TerminateTree { get; init; } = true;
}

public sealed record LoggingPolicyV1
{
    [JsonRequired]
    public bool Enabled { get; init; } = true;

    [JsonRequired]
    public int MaximumFileSizeMb { get; init; } = 25;

    [JsonRequired]
    public int RetainedFiles { get; init; } = 10;

    [JsonRequired]
    public int MaximumTotalSizeMb { get; init; } = 250;
}

public sealed record HealthCheckV1
{
    [JsonRequired]
    public string Type { get; init; } = "process";

    [JsonRequired]
    public string? Target { get; init; }

    [JsonRequired]
    public int IntervalSeconds { get; init; } = 15;

    [JsonRequired]
    public int TimeoutSeconds { get; init; } = 3;
}
