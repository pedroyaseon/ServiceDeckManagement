using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.UnitTests;

internal static class TestServiceDefinitions
{
    public static ServiceDefinitionV1 Valid() =>
        new()
        {
            SchemaVersion = ContractVersions.ServiceDefinitionSchema,
            Id = "example-api",
            DisplayName = "Example API",
            Executable = "apps/ExampleApi/ExampleApi.exe",
            WorkingDirectory = "apps/ExampleApi",
            Arguments = ["--environment", "Production"],
            Environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["APP_ENVIRONMENT"] = "Production"
            },
            SecretReferences = new(StringComparer.OrdinalIgnoreCase),
            StartMode = "automatic",
            RestartPolicy = new()
            {
                Enabled = true,
                MaximumAttempts = 5,
                DelaySeconds = 10,
                MaximumDelaySeconds = 120,
                ResetAfterMinutes = 15
            },
            StopPolicy = new()
            {
                GracefulTimeoutSeconds = 20,
                TerminateTree = true
            },
            Logging = new()
            {
                Enabled = true,
                MaximumFileSizeMb = 25,
                RetainedFiles = 10,
                MaximumTotalSizeMb = 250
            },
            HealthCheck = new()
            {
                Type = "http",
                Target = "http://127.0.0.1:8080/health",
                IntervalSeconds = 15,
                TimeoutSeconds = 3
            }
        };
}
