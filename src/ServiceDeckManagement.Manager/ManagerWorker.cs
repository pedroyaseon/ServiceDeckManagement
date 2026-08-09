using Microsoft.Extensions.Hosting;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerWorker(ManagerPipeServer server) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        server.RunAsync(stoppingToken);
}
