using System.Text.Json;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Host;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.HostTests;

public sealed class ServiceDefinitionLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReturnsValidatedPortablePaths()
    {
        using var root = TemporaryProductRoot.Create();
        var expected = TestServiceDefinition.Create(root, "--emit");
        await WriteDefinitionAsync(root, expected.Definition);
        var resolver = new PortablePathResolver(root);
        var loader = new ServiceDefinitionLoader(
            root,
            resolver,
            new ServiceDefinitionValidator(resolver));

        var result = await loader.LoadAsync("test-service");

        Assert.Equal(expected.ExecutablePath, result.ExecutablePath);
        Assert.Equal(expected.WorkingDirectoryPath, result.WorkingDirectoryPath);
    }

    [Fact]
    public async Task LoadAsync_RejectsMismatchedIdentity()
    {
        using var root = TemporaryProductRoot.Create();
        var expected = TestServiceDefinition.Create(root, "--emit");
        await WriteDefinitionAsync(
            root,
            expected.Definition with { Id = "other-service" });
        var resolver = new PortablePathResolver(root);
        var loader = new ServiceDefinitionLoader(
            root,
            resolver,
            new ServiceDefinitionValidator(resolver));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => loader.LoadAsync("test-service"));
    }

    [Fact]
    public async Task LoadAsync_FailsClosedWhenSecretsRequireManager()
    {
        using var root = TemporaryProductRoot.Create();
        var expected = TestServiceDefinition.Create(root, "--emit");
        var definition = expected.Definition with
        {
            SecretReferences = new(StringComparer.OrdinalIgnoreCase)
            {
                ["API_TOKEN"] = "protected:example"
            }
        };
        await WriteDefinitionAsync(root, definition);
        var resolver = new PortablePathResolver(root);
        var loader = new ServiceDefinitionLoader(
            root,
            resolver,
            new ServiceDefinitionValidator(resolver));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => loader.LoadAsync("test-service"));
    }

    private static async Task WriteDefinitionAsync(
        TemporaryProductRoot root,
        ServiceDefinitionV1 definition)
    {
        var directory = Path.Combine(root.RootPath, "config", "services");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "test-service.json"),
            ServiceDefinitionJson.Serialize(definition),
            new System.Text.UTF8Encoding(false));
    }
}
