using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.HostTests;

internal sealed class TemporaryProductRoot : IProductRootProvider, IDisposable
{
    private TemporaryProductRoot(string rootPath)
    {
        RootPath = rootPath;
        Directory.CreateDirectory(rootPath);
        File.WriteAllLines(
            Path.Combine(rootPath, ".servicedeck-root"),
            ["schemaVersion=1", "product=ServiceDeckManagement"]);
    }

    public string RootPath { get; }

    public static TemporaryProductRoot Create()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"servicedeck-host-test-{Guid.NewGuid():N}");
        return new(path);
    }

    public string CopyTestApplication()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "test-app");
        var destination = Path.Combine(RootPath, "apps", "TestApp");
        CopyDirectory(source, destination);
        return Path.Combine(destination, "ServiceDeckManagement.TestApp.exe");
    }

    public string CopyHostApplication()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "host-app");
        var destination = Path.Combine(RootPath, "app");
        CopyDirectory(source, destination);
        return Path.Combine(destination, "ServiceDeckManagement.Host.exe");
    }

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(RootPath); attempt++)
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException &&
                attempt < 19)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
