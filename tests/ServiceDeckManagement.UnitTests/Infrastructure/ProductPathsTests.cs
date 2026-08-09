using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.UnitTests.Infrastructure;

public sealed class ProductPathsTests
{
    [Fact]
    public void AllOperationalPathsRemainUnderPortableRoot()
    {
        using var directory = TemporaryDirectory.Create();
        var paths = new ProductPaths(new FixedRootProvider(directory.Path));
        var descendants = new[]
        {
            paths.Application,
            paths.Configuration,
            paths.ServiceDefinitions,
            paths.Data,
            paths.Logs,
            paths.Runtime
        };

        Assert.All(
            descendants,
            path => Assert.StartsWith(
                directory.Path,
                path,
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FixedRootProvider(string rootPath)
        : IProductRootProvider
    {
        public string RootPath { get; } = rootPath;
    }
}
