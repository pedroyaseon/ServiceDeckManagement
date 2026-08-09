using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.HostTests;

internal static class TestServiceDefinition
{
    internal static ResolvedServiceDefinition Create(
        TemporaryProductRoot root,
        params string[] arguments)
    {
        var executable = root.CopyTestApplication();
        var workingDirectory = Path.GetDirectoryName(executable)!;
        var definition = new ServiceDefinitionV1
        {
            SchemaVersion = 1,
            Id = "test-service",
            DisplayName = "Test Service",
            Executable = "apps/TestApp/ServiceDeckManagement.TestApp.exe",
            WorkingDirectory = "apps/TestApp",
            Arguments = arguments,
            Environment = new(StringComparer.OrdinalIgnoreCase),
            SecretReferences = new(StringComparer.OrdinalIgnoreCase),
            StartMode = "manual",
            RestartPolicy = new()
            {
                Enabled = false,
                MaximumAttempts = 0,
                DelaySeconds = 1,
                MaximumDelaySeconds = 1,
                ResetAfterMinutes = 1
            },
            StopPolicy = new()
            {
                GracefulTimeoutSeconds = 1,
                TerminateTree = true
            },
            Logging = new()
            {
                Enabled = true,
                MaximumFileSizeMb = 1,
                RetainedFiles = 2,
                MaximumTotalSizeMb = 2
            },
            HealthCheck = new()
            {
                Type = "process",
                IntervalSeconds = 1,
                TimeoutSeconds = 1
            }
        };
        return new(definition, executable, workingDirectory);
    }
}
