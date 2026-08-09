using ServiceDeckManagement.Api;
using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.ApiTests;

public sealed class ApiConfigurationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"sdm-api-config-{Guid.NewGuid():N}");

    [Fact]
    public void MissingConfiguration_UsesSafeLoopbackDefault()
    {
        Directory.CreateDirectory(root);
        var options = new ApiConfiguration(new ProductPaths(new TestRoot(root))).Load();
        Assert.Equal(5180, options.Port);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"bindAddress\":\"0.0.0.0\",\"port\":5180,\"remoteAccess\":false}")]
    [InlineData("{\"schemaVersion\":1,\"bindAddress\":\"127.0.0.1\",\"port\":5180,\"remoteAccess\":true}")]
    [InlineData("{\"schemaVersion\":1,\"bindAddress\":\"127.0.0.1\",\"port\":80,\"remoteAccess\":false}")]
    public void Configuration_RejectsUnsafeNetworkExposure(string json)
    {
        var paths = new ProductPaths(new TestRoot(root));
        Directory.CreateDirectory(paths.Configuration);
        File.WriteAllText(Path.Combine(paths.Configuration, "api.json"), json);

        Assert.Throws<InvalidDataException>(() => new ApiConfiguration(paths).Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed record TestRoot(string RootPath) : IProductRootProvider;
}
