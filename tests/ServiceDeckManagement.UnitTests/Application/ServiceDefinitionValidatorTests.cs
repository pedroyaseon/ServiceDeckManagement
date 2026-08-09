using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.UnitTests.Application;

public sealed class ServiceDefinitionValidatorTests
{
    private readonly ServiceDefinitionValidator validator =
        new(new AcceptingPathResolver());

    [Fact]
    public void Validate_AcceptsValidDefinition()
    {
        var result = validator.Validate(TestServiceDefinitions.Valid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsNullDefinition()
    {
        var result = validator.Validate(null);

        Assert.Contains(
            result.Errors,
            error => error.Code == "definition.required");
    }

    [Fact]
    public void Validate_RejectsUnsupportedSchemaAndReservedId()
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            SchemaVersion = 2,
            Id = "manager"
        };

        var result = validator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Code == "schema.unsupported");
        Assert.Contains(result.Errors, error => error.Code == "id.reserved");
    }

    [Theory]
    [InlineData("PASSWORD")]
    [InlineData("API_KEY")]
    [InlineData("client_secret")]
    public void Validate_RequiresSecretReferenceForSensitiveVariable(
        string variableName)
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            Environment = new(StringComparer.OrdinalIgnoreCase)
            {
                [variableName] = "not-for-plain-text-storage"
            }
        };

        var result = validator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Code == "environment.secret");
    }

    [Fact]
    public void Validate_RejectsRemoteHealthCheckTarget()
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            HealthCheck = new()
            {
                Type = "http",
                Target = "https://example.com/health",
                IntervalSeconds = 15,
                TimeoutSeconds = 3
            }
        };

        var result = validator.Validate(definition);

        Assert.Contains(
            result.Errors,
            error => error.Code == "healthCheck.httpTarget");
    }

    [Fact]
    public void Validate_RejectsCaseInsensitiveEnvironmentDuplicates()
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            Environment = new(StringComparer.Ordinal)
            {
                ["APP_MODE"] = "Production",
                ["app_mode"] = "Development"
            }
        };

        var result = validator.Validate(definition);

        Assert.Contains(
            result.Errors,
            error => error.Code == "environment.duplicateName");
    }

    [Fact]
    public void Validate_RejectsInvalidOperationalLimits()
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            RestartPolicy = new()
            {
                Enabled = true,
                MaximumAttempts = 101,
                DelaySeconds = 30,
                MaximumDelaySeconds = 10,
                ResetAfterMinutes = 0
            },
            Logging = new()
            {
                Enabled = true,
                MaximumFileSizeMb = 500,
                RetainedFiles = 101,
                MaximumTotalSizeMb = 100
            }
        };

        var result = validator.Validate(definition);

        Assert.Contains(
            result.Errors,
            error => error.Code == "restartPolicy.maximumAttempts");
        Assert.Contains(
            result.Errors,
            error => error.Code == "restartPolicy.delay");
        Assert.Contains(
            result.Errors,
            error => error.Code == "logging.limits");
    }

    [Fact]
    public void Validate_RequiresTerminationOfTheWholeProcessTree()
    {
        var definition = TestServiceDefinitions.Valid() with
        {
            StopPolicy = new()
            {
                GracefulTimeoutSeconds = 20,
                TerminateTree = false
            }
        };

        var result = validator.Validate(definition);

        Assert.Contains(
            result.Errors,
            error => error.Code == "stopPolicy.terminateTree");
    }

    private sealed class AcceptingPathResolver : IPortablePathResolver
    {
        public PathResolutionResult Resolve(string? relativePath) =>
            string.IsNullOrWhiteSpace(relativePath)
                ? PathResolutionResult.Failure(
                    "path.required",
                    "O caminho é obrigatório.")
                : PathResolutionResult.Success(relativePath);
    }
}
