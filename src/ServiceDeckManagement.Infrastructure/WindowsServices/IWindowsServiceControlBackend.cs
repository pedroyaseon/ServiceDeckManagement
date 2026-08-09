namespace ServiceDeckManagement.Infrastructure.WindowsServices;

public interface IWindowsServiceControlBackend
{
    WindowsServiceRecord? Query(string serviceName);

    void Create(WindowsServiceRecord service);

    void Update(WindowsServiceRecord service);

    void Delete(string serviceName);

    Task StartAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken);

    Task StopAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken);
}
