using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ServiceDeckManagement.Host.Processes;

/// <summary>
/// Job Object com encerramento automático de toda a árvore ao ser fechado.
/// </summary>
public sealed class WindowsJobObject : IDisposable
{
    private readonly SafeJobHandle handle;
    private bool disposed;

    public WindowsJobObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Job Objects requerem Windows.");
        }

        var rawHandle = WindowsJobNative.CreateJobObject(0, null);
        if (rawHandle == 0 || rawHandle == -1)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        handle = new(rawHandle);

        var information = new WindowsJobNative.JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new()
            {
                LimitFlags = WindowsJobNative.JobObjectLimitKillOnJobClose
            }
        };
        if (!WindowsJobNative.SetInformationJobObject(
                handle,
                WindowsJobNative.JobObjectInformationClass.ExtendedLimitInformation,
                ref information,
                (uint)Marshal.SizeOf(information)))
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error);
        }
    }

    public void Assign(Process process)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        if (!WindowsJobNative.AssignProcessToJobObject(handle, process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    public void Terminate(uint exitCode = 1)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!WindowsJobNative.TerminateJobObject(handle, exitCode))
        {
            var error = Marshal.GetLastPInvokeError();
            const int accessDenied = 5;
            if (error != accessDenied)
            {
                throw new Win32Exception(error);
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        handle.Dispose();
    }
}
