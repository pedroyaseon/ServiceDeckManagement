using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Infrastructure.WindowsServices;

namespace ServiceDeckManagement.ManagerTests;

internal sealed class FakeWindowsServiceBackend : IWindowsServiceControlBackend
{
    internal Dictionary<string, WindowsServiceRecord> Services { get; } =
        new(StringComparer.Ordinal);

    public WindowsServiceRecord? Query(string serviceName) =>
        Services.GetValueOrDefault(serviceName);

    public void Create(WindowsServiceRecord service) => Services.Add(service.ServiceName, service);

    public void Update(WindowsServiceRecord service) => Services[service.ServiceName] = service;

    public void Delete(string serviceName) => Services.Remove(serviceName);

    public Task StartAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _ = timeout;
        cancellationToken.ThrowIfCancellationRequested();
        Services[serviceName] = Services[serviceName] with { State = ManagedServiceState.Running };
        return Task.CompletedTask;
    }

    public Task StopAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _ = timeout;
        cancellationToken.ThrowIfCancellationRequested();
        Services[serviceName] = Services[serviceName] with { State = ManagedServiceState.Stopped };
        return Task.CompletedTask;
    }
}
