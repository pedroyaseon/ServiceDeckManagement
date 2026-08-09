using System.Diagnostics;
using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.Host.Processes;

/// <summary>
/// Processo supervisionado, seus streams e a árvore vinculada ao Job Object.
/// </summary>
public sealed class ManagedProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly WindowsJobObject job;
    private readonly Task standardOutputTask;
    private readonly Task standardErrorTask;
    private int stopping;
    private bool disposed;

    internal ManagedProcess(
        Process process,
        WindowsJobObject job,
        IServiceLogSink logSink)
    {
        this.process = process;
        this.job = job;
        StartedAtUtc = DateTimeOffset.UtcNow;
        standardOutputTask = ProcessOutputPump.RunAsync(
            process.StandardOutput,
            ServiceLogSource.StandardOutput,
            logSink,
            CancellationToken.None);
        standardErrorTask = ProcessOutputPump.RunAsync(
            process.StandardError,
            ServiceLogSource.StandardError,
            logSink,
            CancellationToken.None);
    }

    public int Id => process.Id;

    public DateTimeOffset StartedAtUtc { get; }

    public bool HasExited => process.HasExited;

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var outputTask = Task.WhenAll(standardOutputTask, standardErrorTask);
        var completed = await Task.WhenAny(exitTask, outputTask).ConfigureAwait(false);

        if (completed == outputTask && outputTask.IsFaulted)
        {
            job.Terminate();
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await outputTask.ConfigureAwait(false);
        }

        await exitTask.ConfigureAwait(false);
        var exitCode = process.ExitCode;
        job.Terminate((uint)exitCode);
        await outputTask.ConfigureAwait(false);
        return exitCode;
    }

    public async Task StopAsync(
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref stopping, 1) != 0 || process.HasExited)
        {
            return;
        }

        try
        {
            _ = process.CloseMainWindow();
        }
        catch (InvalidOperationException)
        {
            if (process.HasExited)
            {
                return;
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(gracefulTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                job.Terminate();
                await process.WaitForExitAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            if (!process.HasExited)
            {
                job.Terminate();
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
        }
        finally
        {
            process.Dispose();
            job.Dispose();
        }
    }
}
