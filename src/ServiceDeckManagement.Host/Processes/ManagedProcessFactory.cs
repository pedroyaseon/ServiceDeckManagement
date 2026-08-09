using System.Diagnostics;
using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.Host.Processes;

/// <summary>
/// Inicializa e vincula um processo à contenção do Windows Job Object.
/// </summary>
public sealed class ManagedProcessFactory(
    ProcessStartInfoFactory startInfoFactory,
    IServiceLogSink logSink)
{
    public ManagedProcess Start(ResolvedServiceDefinition service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var job = new WindowsJobObject();
        var process = new Process
        {
            StartInfo = startInfoFactory.Create(service),
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("O Windows recusou a inicialização do processo.");
            }

            try
            {
                job.Assign(process);
            }
            catch
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw;
            }

            return new ManagedProcess(process, job, logSink);
        }
        catch
        {
            process.Dispose();
            job.Dispose();
            throw;
        }
    }
}
