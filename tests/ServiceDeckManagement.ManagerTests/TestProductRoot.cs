using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.ManagerTests;

internal sealed class TestProductRoot : IProductRootProvider, IDisposable
{
    internal TestProductRoot()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"sdm-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        File.WriteAllText(Path.Combine(RootPath, ".servicedeck-root"), "v1\n");
        Directory.CreateDirectory(Path.Combine(RootPath, "app"));
        Directory.CreateDirectory(Path.Combine(RootPath, "apps", "sample"));
        File.WriteAllBytes(
            Path.Combine(RootPath, "app", "ServiceDeckManagement.Host.exe"), [0x4d, 0x5a]);
        File.WriteAllBytes(Path.Combine(RootPath, "apps", "sample", "sample.exe"), [0x4d, 0x5a]);
        Paths = new ProductPaths(this);
    }

    public string RootPath { get; }

    internal ProductPaths Paths { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
