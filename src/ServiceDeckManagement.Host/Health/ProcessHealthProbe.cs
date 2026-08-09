using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.Host.Health;

internal sealed class ProcessHealthProbe : IHealthProbe
{
    public Task<bool> CheckAsync(
        ManagedProcess process,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!process.HasExited);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
