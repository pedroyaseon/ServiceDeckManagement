using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using ServiceDeckManagement.Host.Health;
using ServiceDeckManagement.Host.Logging;
using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.Host;

/// <summary>
/// Supervisiona uma única aplicação durante toda a vida do Serviço do Windows.
/// </summary>
public sealed class ServiceHostWorker(
    ResolvedServiceDefinition service,
    ManagedProcessFactory processFactory,
    IServiceLogSink logSink,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var restartBackoff = new RestartBackoff(service.Definition.RestartPolicy);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var startedAt = timeProvider.GetUtcNow();
                await using var process = processFactory.Start(service);
                await logSink.WriteAsync(
                    ServiceLogSource.System,
                    $"Aplicação iniciada com PID {process.Id}.",
                    stoppingToken).ConfigureAwait(false);

                using var healthCancellation = CancellationTokenSource
                    .CreateLinkedTokenSource(stoppingToken);
                var healthTask = MonitorHealthAsync(
                    process,
                    healthCancellation.Token);

                int exitCode;
                try
                {
                    exitCode = await process.WaitForExitAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    healthCancellation.Cancel();
                    await IgnoreCancellationAsync(healthTask).ConfigureAwait(false);
                    await logSink.WriteAsync(
                        ServiceLogSource.System,
                        "Parada solicitada; iniciando encerramento gracioso.",
                        CancellationToken.None).ConfigureAwait(false);
                    await process.StopAsync(
                        TimeSpan.FromSeconds(
                            service.Definition.StopPolicy.GracefulTimeoutSeconds),
                        CancellationToken.None).ConfigureAwait(false);
                    await logSink.WriteAsync(
                        ServiceLogSource.System,
                        "Aplicação encerrada.",
                        CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                healthCancellation.Cancel();
                await IgnoreCancellationAsync(healthTask).ConfigureAwait(false);

                var runtime = timeProvider.GetUtcNow() - startedAt;
                await logSink.WriteAsync(
                    ServiceLogSource.System,
                    $"Aplicação finalizada com código {exitCode} após {runtime.TotalSeconds:F1} s.",
                    stoppingToken).ConfigureAwait(false);

                if (!restartBackoff.TryGetNextDelay(runtime, out var delay))
                {
                    Environment.ExitCode = exitCode == 0 ? 1 : exitCode;
                    await logSink.WriteAsync(
                        ServiceLogSource.System,
                        "Política de reinício esgotada; o Service Host será interrompido.",
                        stoppingToken).ConfigureAwait(false);
                    applicationLifetime.StopApplication();
                    return;
                }

                await logSink.WriteAsync(
                    ServiceLogSource.System,
                    $"Reinício {restartBackoff.Attempts} agendado em {delay.TotalSeconds:F0} s.",
                    stoppingToken).ConfigureAwait(false);
                await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento cooperativo do host.
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            await logSink.WriteAsync(
                ServiceLogSource.System,
                $"Falha do supervisor: {ToSafeFailureName(exception)}.",
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task MonitorHealthAsync(
        ManagedProcess process,
        CancellationToken cancellationToken)
    {
        await using var probe = HealthProbeFactory.Create(
            service.Definition.HealthCheck);
        var interval = TimeSpan.FromSeconds(
            service.Definition.HealthCheck.IntervalSeconds);
        var timeout = TimeSpan.FromSeconds(
            service.Definition.HealthCheck.TimeoutSeconds);

        using var timer = new PeriodicTimer(interval, timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            using var checkCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            checkCancellation.CancelAfter(timeout);

            var healthy = false;
            try
            {
                healthy = await probe.CheckAsync(
                    process,
                    checkCancellation.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is
                HttpRequestException or
                SocketException or
                OperationCanceledException)
            {
                healthy = false;
            }

            if (!healthy)
            {
                await logSink.WriteAsync(
                    ServiceLogSource.System,
                    "Health check sem resposta saudável.",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento esperado ao finalizar uma tentativa.
        }
    }

    private static string ToSafeFailureName(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "acesso negado",
        FileNotFoundException => "arquivo não encontrado",
        DirectoryNotFoundException => "diretório não encontrado",
        IOException => "falha de entrada e saída",
        System.ComponentModel.Win32Exception => "falha de processo do Windows",
        _ => "falha interna"
    };
}
