using System.ComponentModel;
using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Launcher;

namespace ServiceDeckManagement.LauncherTests;

public sealed class ManagerSetupServiceTests
{
    [Fact]
    public async Task Install_UsesElevatedHelperWithCurrentUserSid()
    {
        using var root = new SetupRoot(includeBinaries: true);
        var runner = new RecordingRunner { ExitCode = 0 };
        var service = new ManagerSetupService(
            root.Paths,
            runner,
            new FixedIdentity("S-1-5-21-10-20-30-50"));

        var outcome = await service.InstallOrRepairAsync(CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(root.Paths.SetupExecutable, runner.Executable);
        Assert.Equal(root.RootPath, runner.WorkingDirectory);
        Assert.Equal("S-1-5-21-10-20-30-50", runner.LauncherSid);
    }

    [Fact]
    public async Task Install_RejectsIncompletePackageWithoutStartingElevation()
    {
        using var root = new SetupRoot(includeBinaries: false);
        var runner = new RecordingRunner();
        var service = new ManagerSetupService(
            root.Paths,
            runner,
            new FixedIdentity("S-1-5-21-10-20-30-50"));

        var outcome = await service.InstallOrRepairAsync(CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Install_MapsUacCancellationWithoutExposingNativeDetails()
    {
        using var root = new SetupRoot(includeBinaries: true);
        var service = new ManagerSetupService(
            root.Paths,
            new RecordingRunner { Exception = new Win32Exception(1223, "native detail") },
            new FixedIdentity("S-1-5-21-10-20-30-50"));

        var outcome = await service.InstallOrRepairAsync(CancellationToken.None);

        Assert.True(outcome.Cancelled);
        Assert.DoesNotContain("native detail", outcome.Message, StringComparison.Ordinal);
    }

    private sealed class SetupRoot : IProductRootProvider, IDisposable
    {
        internal SetupRoot(bool includeBinaries)
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"sdm-launcher-setup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(RootPath, "app"));
            Paths = new ProductPaths(this);
            if (includeBinaries)
            {
                File.WriteAllBytes(Paths.ManagerExecutable, [0x4d, 0x5a]);
                File.WriteAllBytes(Paths.SetupExecutable, [0x4d, 0x5a]);
            }
        }
        public string RootPath { get; }
        internal ProductPaths Paths { get; }
        public void Dispose()
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed class FixedIdentity(string sid) : ICurrentWindowsIdentity
    {
        public string GetUserSid() => sid;
    }

    private sealed class RecordingRunner : IElevatedSetupRunner
    {
        internal int ExitCode { get; init; }
        internal Exception? Exception { get; init; }
        internal int Calls { get; private set; }
        internal string? Executable { get; private set; }
        internal string? WorkingDirectory { get; private set; }
        internal string? LauncherSid { get; private set; }

        public Task<int> RunAsync(
            string executable,
            string workingDirectory,
            string launcherSid,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Executable = executable;
            WorkingDirectory = workingDirectory;
            LauncherSid = launcherSid;
            if (Exception is not null) throw Exception;
            return Task.FromResult(ExitCode);
        }
    }
}
