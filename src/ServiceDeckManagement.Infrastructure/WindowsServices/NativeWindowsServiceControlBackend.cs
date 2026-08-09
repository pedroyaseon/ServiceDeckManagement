using System.ComponentModel;
using System.Runtime.InteropServices;
using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Infrastructure.WindowsServices;

/// <summary>
/// Adaptador local do SCM. Não aceita nomes de máquina e não executa shell.
/// </summary>
public sealed class NativeWindowsServiceControlBackend : IWindowsServiceControlBackend
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerCreateService = 0x0002;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint DeleteAccess = 0x00010000;
    private const uint ServiceAllRequired =
        ServiceQueryConfig | ServiceChangeConfig | ServiceQueryStatus |
        ServiceStart | ServiceStop | DeleteAccess;
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceErrorNormal = 0x00000001;
    private const uint ServiceNoChange = 0xffffffff;
    private const uint ServiceConfigDescription = 1;
    private const uint ScStatusProcessInfo = 0;
    private const uint ServiceControlStop = 1;
    private const int ErrorInsufficientBuffer = 122;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceNotActive = 1062;

    public WindowsServiceRecord? Query(string serviceName)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect);
        using var service = NativeServiceMethods.OpenService(
            scm, serviceName, ServiceQueryConfig | ServiceQueryStatus);
        if (service.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorServiceDoesNotExist)
            {
                return null;
            }

            throw new Win32Exception(error, "Falha ao abrir o serviço no SCM.");
        }

        var config = ReadConfig(service);
        var status = ReadStatus(service);
        return new(
            serviceName,
            config.DisplayName,
            config.BinaryPath,
            ReadDescription(service),
            config.StartType,
            MapState(status.CurrentState),
            status.ProcessId == 0 ? null : checked((int)status.ProcessId));
    }

    public void Create(WindowsServiceRecord service)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect | ScManagerCreateService);
        using var handle = NativeServiceMethods.CreateService(
            scm,
            service.ServiceName,
            service.DisplayName,
            ServiceAllRequired,
            ServiceWin32OwnProcess,
            service.StartType,
            ServiceErrorNormal,
            service.BinaryPath,
            null,
            0,
            null,
            null,
            null);
        ThrowIfInvalid(handle, "Falha ao criar o serviço no SCM.");
        try
        {
            SetDescription(handle, service.Description);
        }
        catch
        {
            _ = NativeServiceMethods.DeleteService(handle);
            throw;
        }
    }

    public void Update(WindowsServiceRecord service)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect);
        using var handle = OpenRequiredService(scm, service.ServiceName, ServiceChangeConfig);
        if (!NativeServiceMethods.ChangeServiceConfig(
                handle,
                ServiceNoChange,
                service.StartType,
                ServiceNoChange,
                service.BinaryPath,
                null,
                0,
                null,
                null,
                null,
                service.DisplayName))
        {
            throw LastError("Falha ao atualizar o serviço no SCM.");
        }

        SetDescription(handle, service.Description);
    }

    public void Delete(string serviceName)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect);
        using var service = OpenRequiredService(scm, serviceName, DeleteAccess);
        if (!NativeServiceMethods.DeleteService(service))
        {
            throw LastError("Falha ao remover o serviço do SCM.");
        }
    }

    public async Task StartAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect);
        using var service = OpenRequiredService(
            scm, serviceName, ServiceStart | ServiceQueryStatus);
        if (!NativeServiceMethods.StartService(service, 0, 0))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorServiceAlreadyRunning)
            {
                throw new Win32Exception(error, "Falha ao iniciar o serviço.");
            }
        }

        await WaitForStateAsync(
            service, ManagedServiceState.Running, timeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        EnsureWindows();
        using var scm = OpenScm(ScManagerConnect);
        using var service = OpenRequiredService(
            scm, serviceName, ServiceStop | ServiceQueryStatus);
        if (!NativeServiceMethods.ControlService(
                service, ServiceControlStop, out _))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorServiceNotActive)
            {
                throw new Win32Exception(error, "Falha ao parar o serviço.");
            }
        }

        await WaitForStateAsync(
            service, ManagedServiceState.Stopped, timeout, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForStateAsync(
        SafeServiceHandle service,
        ManagedServiceState expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (MapState(ReadStatus(service).CurrentState) != expected)
        {
            if (TimeProvider.System.GetElapsedTime(started) >= timeout)
            {
                throw new TimeoutException("O SCM não concluiu a transição no prazo esperado.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static SafeServiceHandle OpenScm(uint access)
    {
        var handle = NativeServiceMethods.OpenSCManager(null, null, access);
        ThrowIfInvalid(handle, "Falha ao abrir o SCM local.");
        return handle;
    }

    private static SafeServiceHandle OpenRequiredService(
        SafeServiceHandle scm,
        string name,
        uint access)
    {
        var handle = NativeServiceMethods.OpenService(scm, name, access);
        ThrowIfInvalid(handle, "Falha ao abrir o serviço gerenciado.");
        return handle;
    }

    private static (uint StartType, string BinaryPath, string DisplayName) ReadConfig(
        SafeServiceHandle service)
    {
        _ = NativeServiceMethods.QueryServiceConfig(service, 0, 0, out var size);
        var error = Marshal.GetLastPInvokeError();
        if (error != ErrorInsufficientBuffer || size == 0)
        {
            throw new Win32Exception(error, "Falha ao consultar a configuração do serviço.");
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            if (!NativeServiceMethods.QueryServiceConfig(service, buffer, size, out _))
            {
                throw LastError("Falha ao consultar a configuração do serviço.");
            }

            var value = Marshal.PtrToStructure<NativeServiceMethods.QueryServiceConfigData>(buffer);
            return (
                value.StartType,
                Marshal.PtrToStringUni(value.BinaryPathName) ?? string.Empty,
                Marshal.PtrToStringUni(value.DisplayName) ?? string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadDescription(SafeServiceHandle service)
    {
        _ = NativeServiceMethods.QueryServiceConfig2(
            service, ServiceConfigDescription, 0, 0, out var size);
        var error = Marshal.GetLastPInvokeError();
        if (error != ErrorInsufficientBuffer || size == 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            if (!NativeServiceMethods.QueryServiceConfig2(
                    service, ServiceConfigDescription, buffer, size, out _))
            {
                throw LastError("Falha ao consultar a descrição do serviço.");
            }

            var value = Marshal.PtrToStructure<NativeServiceMethods.ServiceDescription>(buffer);
            return Marshal.PtrToStringUni(value.Description) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static NativeServiceMethods.ServiceStatusProcess ReadStatus(
        SafeServiceHandle service)
    {
        if (!NativeServiceMethods.QueryServiceStatusEx(
                service,
                ScStatusProcessInfo,
                out var status,
                checked((uint)Marshal.SizeOf<NativeServiceMethods.ServiceStatusProcess>()),
                out _))
        {
            throw LastError("Falha ao consultar o estado do serviço.");
        }

        return status;
    }

    private static void SetDescription(SafeServiceHandle service, string description)
    {
        var text = Marshal.StringToHGlobalUni(description);
        var data = Marshal.AllocHGlobal(
            Marshal.SizeOf<NativeServiceMethods.ServiceDescription>());
        try
        {
            Marshal.StructureToPtr(
                new NativeServiceMethods.ServiceDescription { Description = text },
                data,
                fDeleteOld: false);
            if (!NativeServiceMethods.ChangeServiceConfig2(
                    service, ServiceConfigDescription, data))
            {
                throw LastError("Falha ao gravar a identidade do serviço.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(data);
            Marshal.FreeHGlobal(text);
        }
    }

    private static ManagedServiceState MapState(uint state) => state switch
    {
        1 => ManagedServiceState.Stopped,
        2 => ManagedServiceState.StartPending,
        3 => ManagedServiceState.StopPending,
        4 => ManagedServiceState.Running,
        5 => ManagedServiceState.ContinuePending,
        6 => ManagedServiceState.PausePending,
        7 => ManagedServiceState.Paused,
        _ => ManagedServiceState.Unknown
    };

    private static void ThrowIfInvalid(SafeServiceHandle handle, string message)
    {
        if (handle.IsInvalid)
        {
            throw LastError(message);
        }
    }

    private static Win32Exception LastError(string message) =>
        new(Marshal.GetLastPInvokeError(), message);

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("O SCM requer Windows.");
        }
    }
}
