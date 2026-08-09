using Microsoft.AspNetCore.SignalR.Client;
using ServiceDeckManagement.Contracts.Api;

namespace ServiceDeckManagement.Launcher;

public sealed class RealtimeService(LauncherOptions options, Func<string?> accessToken) : IAsyncDisposable
{
    private HubConnection? connection;

    public event Action<ServiceSnapshotEnvelopeV1>? SnapshotReceived;

    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (connection is not null) return;
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(options.ApiBaseUri, "api/v1/events"), http =>
                http.AccessTokenProvider = () => Task.FromResult(accessToken()))
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)])
            .Build();
        connection.On<ServiceSnapshotEnvelopeV1>("services.snapshot", snapshot => SnapshotReceived?.Invoke(snapshot));
        connection.Reconnecting += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            ConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };
        connection.Closed += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        try
        {
            await connection.StartAsync(cancellationToken).ConfigureAwait(false);
            ConnectionChanged?.Invoke(true);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is null) return;
        await connection.DisposeAsync().ConfigureAwait(false);
        connection = null;
    }
}
