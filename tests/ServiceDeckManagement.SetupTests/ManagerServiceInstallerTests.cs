using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Infrastructure.WindowsServices;
using ServiceDeckManagement.Setup;

namespace ServiceDeckManagement.SetupTests;

public sealed class ManagerServiceInstallerTests
{
    [Fact]
    public async Task Install_CreatesOwnedAutomaticServiceAndPreservesApiSid()
    {
        using var root = new SetupRoot();
        var backend = new RecordingBackend();
        var security = new RecordingSecurity(
            new ManagerSecurityOptions("S-1-5-21-10-20-30-40", null));
        var installer = new ManagerServiceInstaller(root.Paths, backend, security);

        await installer.InstallOrRepairAsync(
            "S-1-5-21-10-20-30-50", CancellationToken.None);

        var service = Assert.Single(backend.Services.Values);
        Assert.Equal(ManagerServiceInstaller.ServiceName, service.ServiceName);
        Assert.Equal(ManagerServiceInstaller.OwnershipMarker, service.Description);
        Assert.Equal(2U, service.StartType);
        Assert.Equal($"\"{root.Paths.ManagerExecutable}\"", service.BinaryPath);
        Assert.Equal(ManagedServiceState.Running, service.State);
        Assert.Equal("S-1-5-21-10-20-30-40", security.Saved?.ApiClientSid);
        Assert.Equal("S-1-5-21-10-20-30-50", security.Saved?.LauncherClientSid);
    }

    [Fact]
    public async Task Repair_StopsUpdatesAndRestartsOwnedService()
    {
        using var root = new SetupRoot();
        var backend = new RecordingBackend();
        backend.Services[ManagerServiceInstaller.ServiceName] = new(
            ManagerServiceInstaller.ServiceName,
            "Antigo",
            "\"C:\\old\\manager.exe\"",
            ManagerServiceInstaller.OwnershipMarker,
            3,
            ManagedServiceState.Running,
            42);
        var installer = new ManagerServiceInstaller(
            root.Paths,
            backend,
            new RecordingSecurity(ManagerSecurityOptions.LocalAdministratorsOnly));

        await installer.InstallOrRepairAsync(
            "S-1-5-21-10-20-30-50", CancellationToken.None);

        Assert.Equal(["stop", "update", "start"], backend.Actions);
        Assert.Equal(
            $"\"{root.Paths.ManagerExecutable}\"",
            backend.Services[ManagerServiceInstaller.ServiceName].BinaryPath);
    }

    [Fact]
    public async Task Install_RefusesExistingServiceWithoutOwnershipMarker()
    {
        using var root = new SetupRoot();
        var backend = new RecordingBackend();
        backend.Services[ManagerServiceInstaller.ServiceName] = new(
            ManagerServiceInstaller.ServiceName,
            "Outro produto",
            "\"C:\\other.exe\"",
            "other:marker",
            2,
            ManagedServiceState.Stopped,
            null);
        var security = new RecordingSecurity(ManagerSecurityOptions.LocalAdministratorsOnly);
        var installer = new ManagerServiceInstaller(root.Paths, backend, security);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            installer.InstallOrRepairAsync(
                "S-1-5-21-10-20-30-50", CancellationToken.None));

        Assert.Null(security.Saved);
        Assert.Empty(backend.Actions);
    }

    private sealed class SetupRoot : IProductRootProvider, IDisposable
    {
        internal SetupRoot()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"sdm-setup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(RootPath, "app"));
            Paths = new ProductPaths(this);
            File.WriteAllBytes(Paths.ManagerExecutable, [0x4d, 0x5a]);
            File.WriteAllBytes(Paths.SetupExecutable, [0x4d, 0x5a]);
        }

        public string RootPath { get; }
        internal ProductPaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed class RecordingSecurity(ManagerSecurityOptions current) : IManagerSetupSecurity
    {
        internal ManagerSecurityOptions? Saved { get; private set; }
        public ManagerSecurityOptions Load() => current;
        public void SaveAndHarden(ManagerSecurityOptions options) => Saved = options;
    }

    private sealed class RecordingBackend : IWindowsServiceControlBackend
    {
        internal Dictionary<string, WindowsServiceRecord> Services { get; } =
            new(StringComparer.Ordinal);
        internal List<string> Actions { get; } = [];
        public WindowsServiceRecord? Query(string serviceName) =>
            Services.GetValueOrDefault(serviceName);
        public void Create(WindowsServiceRecord service)
        {
            Actions.Add("create");
            Services.Add(service.ServiceName, service);
        }
        public void Update(WindowsServiceRecord service)
        {
            Actions.Add("update");
            Services[service.ServiceName] = service;
        }
        public void Delete(string serviceName) => Services.Remove(serviceName);
        public Task StartAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _ = timeout;
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add("start");
            Services[serviceName] = Services[serviceName] with { State = ManagedServiceState.Running };
            return Task.CompletedTask;
        }
        public Task StopAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _ = timeout;
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add("stop");
            Services[serviceName] = Services[serviceName] with { State = ManagedServiceState.Stopped };
            return Task.CompletedTask;
        }
    }
}
