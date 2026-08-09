using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.Host.Health;

/// <summary>
/// Verificação limitada de saúde da aplicação supervisionada.
/// </summary>
public interface IHealthProbe : IAsyncDisposable
{
    Task<bool> CheckAsync(
        ManagedProcess process,
        CancellationToken cancellationToken);
}
