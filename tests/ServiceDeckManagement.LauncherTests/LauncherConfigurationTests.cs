using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Launcher;

namespace ServiceDeckManagement.LauncherTests;

public sealed class LauncherConfigurationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"sdm-launcher-config-{Guid.NewGuid():N}");

    [Fact]
    public void MissingConfiguration_UsesSafeLoopbackDefault()
    {
        Directory.CreateDirectory(root);

        var options = new LauncherConfiguration(new ProductPaths(new TestRoot(root))).Load();

        Assert.Equal(new Uri("http://127.0.0.1:5180/"), options.ApiBaseUri);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"apiBaseUrl\":\"http://192.168.1.10:5180/\"}")]
    [InlineData("{\"schemaVersion\":1,\"apiBaseUrl\":\"https://127.0.0.1:5180/\"}")]
    [InlineData("{\"schemaVersion\":1,\"apiBaseUrl\":\"http://127.0.0.1:80/\"}")]
    [InlineData("{\"schemaVersion\":1,\"apiBaseUrl\":\"http://127.0.0.1:5180/path\"}")]
    [InlineData("{\"schemaVersion\":1,\"apiBaseUrl\":\"http://127.0.0.1:5180/?token=secret\"}")]
    [InlineData("{\"schemaVersion\":2,\"apiBaseUrl\":\"http://127.0.0.1:5180/\"}")]
    public void Configuration_RejectsUnsupportedOrUnsafeAddress(string json)
    {
        var paths = new ProductPaths(new TestRoot(root));
        Directory.CreateDirectory(paths.Configuration);
        File.WriteAllText(Path.Combine(paths.Configuration, "launcher.json"), json);

        Assert.Throws<InvalidDataException>(() => new LauncherConfiguration(paths).Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed record TestRoot(string RootPath) : IProductRootProvider;
}
