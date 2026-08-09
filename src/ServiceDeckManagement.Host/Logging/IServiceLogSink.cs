namespace ServiceDeckManagement.Host.Logging;

/// <summary>
/// Destino limitado para eventos produzidos pelo Service Host.
/// </summary>
public interface IServiceLogSink : IAsyncDisposable
{
    ValueTask WriteAsync(
        ServiceLogSource source,
        string message,
        CancellationToken cancellationToken = default);
}
