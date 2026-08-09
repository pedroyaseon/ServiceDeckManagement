using System.Net.Sockets;
using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.Host.Health;

internal sealed class TcpHealthProbe(string host, int port) : IHealthProbe
{
    public async Task<bool> CheckAsync(
        ManagedProcess process,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return false;
        }

        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        return client.Connected;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
