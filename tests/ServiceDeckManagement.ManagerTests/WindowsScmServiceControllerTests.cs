using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Infrastructure.WindowsServices;

namespace ServiceDeckManagement.ManagerTests;

public sealed class WindowsScmServiceControllerTests
{
    [Fact]
    public async Task Install_UsesOwnedNameMarkerAndDirectHostCommand()
    {
        using var root = new TestProductRoot();
        var backend = new FakeWindowsServiceBackend();
        var controller = new WindowsScmServiceController(root.Paths, backend);

        await controller.InstallAsync(TestDefinitions.Create(), CancellationToken.None);

        var record = Assert.Single(backend.Services).Value;
        Assert.Equal("ServiceDeckManagement.Managed.sample", record.ServiceName);
        Assert.Equal("ServiceDeckManagement:v1:sample", record.Description);
        Assert.Contains("ServiceDeckManagement.Host.exe\" --service-id sample",
            record.BinaryPath, StringComparison.Ordinal);
        Assert.DoesNotContain("nssm", record.BinaryPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MutatingOperation_RejectsForeignRegistration()
    {
        using var root = new TestProductRoot();
        var backend = new FakeWindowsServiceBackend();
        var controller = new WindowsScmServiceController(root.Paths, backend);
        await controller.InstallAsync(TestDefinitions.Create(), CancellationToken.None);
        var name = "ServiceDeckManagement.Managed.sample";
        backend.Services[name] = backend.Services[name] with { Description = "foreign" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.StartAsync(TestDefinitions.Create(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.RemoveAsync(TestDefinitions.Create(), CancellationToken.None));
    }

    [Fact]
    public async Task Repair_RestoresMutableConfigurationButNotForeignMarker()
    {
        using var root = new TestProductRoot();
        var backend = new FakeWindowsServiceBackend();
        var controller = new WindowsScmServiceController(root.Paths, backend);
        var definition = TestDefinitions.Create();
        await controller.InstallAsync(definition, CancellationToken.None);
        var name = "ServiceDeckManagement.Managed.sample";
        backend.Services[name] = backend.Services[name] with { BinaryPath = "broken" };

        await controller.RepairAsync(definition, CancellationToken.None);
        Assert.Contains("ServiceDeckManagement.Host.exe", backend.Services[name].BinaryPath,
            StringComparison.Ordinal);

        backend.Services[name] = backend.Services[name] with { Description = "foreign" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.RepairAsync(definition, CancellationToken.None));
    }

    [Theory]
    [InlineData(ManagerRole.Viewer, ManagerOperationsV1.Inventory, true)]
    [InlineData(ManagerRole.Viewer, ManagerOperationsV1.Details, true)]
    [InlineData(ManagerRole.Viewer, ManagerOperationsV1.Logs, true)]
    [InlineData(ManagerRole.Viewer, ManagerOperationsV1.Start, false)]
    [InlineData(ManagerRole.Operator, ManagerOperationsV1.Restart, true)]
    [InlineData(ManagerRole.Operator, ManagerOperationsV1.Remove, false)]
    [InlineData(ManagerRole.Administrator, ManagerOperationsV1.Remove, true)]
    public void Authorization_UsesServerDerivedRole(
        ManagerRole role,
        string operation,
        bool expected) =>
        Assert.Equal(expected, ManagerAuthorization.IsAllowed(role, operation));
}
