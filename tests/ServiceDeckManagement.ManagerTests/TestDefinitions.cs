using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.ManagerTests;

internal static class TestDefinitions
{
    internal static ServiceDefinitionV1 Create(
        string id = "sample",
        string displayName = "Sample") => new()
        {
            SchemaVersion = 1,
            Id = id,
            DisplayName = displayName,
            Executable = "apps/sample/sample.exe",
            WorkingDirectory = "apps/sample",
            Arguments = [],
            Environment = new(StringComparer.OrdinalIgnoreCase),
            SecretReferences = new(StringComparer.OrdinalIgnoreCase),
            StartMode = "manual",
            RestartPolicy = new()
            {
                Enabled = true,
                MaximumAttempts = 5,
                DelaySeconds = 1,
                MaximumDelaySeconds = 10,
                ResetAfterMinutes = 15
            },
            StopPolicy = new()
            {
                GracefulTimeoutSeconds = 10,
                TerminateTree = true
            },
            Logging = new()
            {
                Enabled = true,
                MaximumFileSizeMb = 5,
                RetainedFiles = 3,
                MaximumTotalSizeMb = 15
            },
            HealthCheck = new()
            {
                Type = "process",
                IntervalSeconds = 5,
                TimeoutSeconds = 2
            }
        };
}
