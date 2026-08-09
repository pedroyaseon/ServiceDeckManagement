using System.Diagnostics;
using ServiceDeckManagement.Host.Logging;
using ServiceDeckManagement.Host.Processes;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.HostTests;

public sealed class ManagedProcessTests
{
    [Fact]
    public async Task Start_CapturesBothStreamsWithoutAnsi()
    {
        using var root = TemporaryProductRoot.Create();
        var service = TestServiceDefinition.Create(
            root,
            "--emit",
            "value with spaces",
            "& whoami");
        var sink = new CollectingLogSink();
        var factory = new ManagedProcessFactory(
            new ProcessStartInfoFactory(new PortablePathResolver(root)),
            sink);

        await using var process = factory.Start(service);
        var exitCode = await process.WaitForExitAsync(CancellationToken.None);

        Assert.Equal(7, exitCode);
        Assert.Contains(
            sink.Entries,
            item => item is (ServiceLogSource.StandardOutput, "saída limpa"));
        Assert.Contains(
            sink.Entries,
            item => item is (ServiceLogSource.StandardError, "erro controlado"));
        Assert.Contains(
            sink.Entries,
            item => item.Message == "value with spaces|& whoami");
    }

    [Fact]
    public async Task DisposeAsync_TerminatesTheJobProcessTree()
    {
        using var root = TemporaryProductRoot.Create();
        var childPidPath = Path.Combine(root.RootPath, "child.pid");
        var service = TestServiceDefinition.Create(
            root,
            "--spawn-child",
            childPidPath);
        var sink = new CollectingLogSink();
        var factory = new ManagedProcessFactory(
            new ProcessStartInfoFactory(new PortablePathResolver(root)),
            sink);

        var managed = factory.Start(service);
        var childPid = await WaitForPidAsync(childPidPath, CancellationToken.None);

        await managed.DisposeAsync();

        Assert.True(await HasExitedAsync(childPid));
    }

    [Fact]
    public async Task WaitForExitAsync_TerminatesChildrenLeftByMainProcess()
    {
        using var root = TemporaryProductRoot.Create();
        var childPidPath = Path.Combine(root.RootPath, "orphan.pid");
        var service = TestServiceDefinition.Create(
            root,
            "--spawn-child-and-exit",
            childPidPath);
        var factory = new ManagedProcessFactory(
            new ProcessStartInfoFactory(new PortablePathResolver(root)),
            new CollectingLogSink());

        await using var managed = factory.Start(service);
        var childPid = await WaitForPidAsync(childPidPath, CancellationToken.None);
        var exitCode = await managed.WaitForExitAsync(CancellationToken.None);

        Assert.Equal(9, exitCode);
        Assert.True(await HasExitedAsync(childPid));
    }

    private static async Task WaitForFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!File.Exists(path) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, cancellationToken);
        }

        Assert.True(File.Exists(path), "O processo filho não publicou seu PID.");
    }

    private static async Task<int> WaitForPidAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await WaitForFileAsync(path, cancellationToken);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken);
                if (int.TryParse(
                    text,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var processId))
                {
                    return processId;
                }
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                // O processo pode ter criado o arquivo antes de concluir o flush.
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException("O processo filho não concluiu a publicação do PID.");
    }

    private static async Task<bool> HasExitedAsync(int processId)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }
}
