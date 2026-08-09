using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Infrastructure.LocalProtocol;

namespace ServiceDeckManagement.Api;

[Authorize]
public sealed class ServiceEventsHub : Hub;

public sealed class ServiceSnapshotState
{
    private readonly object sync = new();
    private ServiceSnapshotEnvelopeV1 current = new()
    {
        Sequence = 0,
        GeneratedAt = DateTimeOffset.UtcNow,
        Services = []
    };

    public ServiceSnapshotEnvelopeV1 Current
    {
        get
        {
            lock (sync)
            {
                return current;
            }
        }
    }

    public ServiceSnapshotEnvelopeV1 Update(IReadOnlyList<ManagedServiceSnapshotV1> services)
    {
        lock (sync)
        {
            current = new()
            {
                Sequence = current.Sequence + 1,
                GeneratedAt = DateTimeOffset.UtcNow,
                Services = services
            };
            return current;
        }
    }
}

public sealed class ServiceSnapshotWorker(
    IManagerClient manager,
    ServiceSnapshotState state,
    IHubContext<ServiceEventsHub> hub,
    ILogger<ServiceSnapshotWorker> logger) : BackgroundService
{
    private const string SystemActor = "00000000-0000-0000-0000-000000000001";
    private static readonly Action<ILogger, Exception?> ManagerUnavailable = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(1001, nameof(ManagerUnavailable)),
        "Manager indisponível durante a atualização em tempo real.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await manager.SendAsync(
                    ManagerOperationsV1.Inventory,
                    new { },
                    SystemActor,
                    ApiRolesV1.Viewer,
                    stoppingToken).ConfigureAwait(false);
                if (response.Success && response.Data is { } data)
                {
                    var services = data.Deserialize<IReadOnlyList<ManagedServiceSnapshotV1>>(ManagerJson.Options) ?? [];
                    var snapshot = state.Update(services);
                    await hub.Clients.All.SendAsync("services.snapshot", snapshot, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    ManagerUnavailable(logger, exception);
                }
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }
}
