using System.Runtime.InteropServices;

namespace ServiceDeckManagement.Infrastructure.WindowsServices;

internal static class NativeServiceMethods
{
    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    internal static extern SafeServiceHandle OpenSCManager(
        string? machineName,
        string? databaseName,
        uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    internal static extern SafeServiceHandle OpenService(
        SafeServiceHandle scm,
        string serviceName,
        uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "CreateServiceW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    internal static extern SafeServiceHandle CreateService(
        SafeServiceHandle scm,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        nint tagId,
        string? dependencies,
        string? serviceStartName,
        string? password);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeServiceConfig(
        SafeServiceHandle service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        nint tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string displayName);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeServiceConfig2(
        SafeServiceHandle service,
        uint infoLevel,
        nint info);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceConfig(
        SafeServiceHandle service,
        nint serviceConfig,
        uint bufferSize,
        out uint bytesNeeded);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceConfig2(
        SafeServiceHandle service,
        uint infoLevel,
        nint buffer,
        uint bufferSize,
        out uint bytesNeeded);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceStatusEx(
        SafeServiceHandle service,
        uint infoLevel,
        out ServiceStatusProcess buffer,
        uint bufferSize,
        out uint bytesNeeded);

    [DllImport("advapi32.dll", EntryPoint = "StartServiceW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool StartService(
        SafeServiceHandle service,
        uint argumentCount,
        nint arguments);

    [DllImport("advapi32.dll", EntryPoint = "ControlService", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ControlService(
        SafeServiceHandle service,
        uint control,
        out ServiceStatus status);

    [DllImport("advapi32.dll", EntryPoint = "DeleteService", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteService(SafeServiceHandle service);

    [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseServiceHandle(nint serviceHandle);

    [StructLayout(LayoutKind.Sequential)]
    internal struct QueryServiceConfigData
    {
        internal uint ServiceType;
        internal uint StartType;
        internal uint ErrorControl;
        internal nint BinaryPathName;
        internal nint LoadOrderGroup;
        internal uint TagId;
        internal nint Dependencies;
        internal nint ServiceStartName;
        internal nint DisplayName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceDescription
    {
        internal nint Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceStatusProcess
    {
        internal uint ServiceType;
        internal uint CurrentState;
        internal uint ControlsAccepted;
        internal uint Win32ExitCode;
        internal uint ServiceSpecificExitCode;
        internal uint CheckPoint;
        internal uint WaitHint;
        internal uint ProcessId;
        internal uint ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceStatus
    {
        internal uint ServiceType;
        internal uint CurrentState;
        internal uint ControlsAccepted;
        internal uint Win32ExitCode;
        internal uint ServiceSpecificExitCode;
        internal uint CheckPoint;
        internal uint WaitHint;
    }
}
