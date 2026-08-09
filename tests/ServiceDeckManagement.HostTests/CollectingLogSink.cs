using System.Collections.Concurrent;
using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.HostTests;

internal sealed class CollectingLogSink : IServiceLogSink
{
    internal ConcurrentQueue<(ServiceLogSource Source, string Message)> Entries { get; } = new();

    public ValueTask WriteAsync(
        ServiceLogSource source,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Enqueue((source, AnsiTextSanitizer.Sanitize(message)));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
