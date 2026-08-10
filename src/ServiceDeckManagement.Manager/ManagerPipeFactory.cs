using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerPipeFactory(ManagerSecurityOptions securityOptions)
{
    public const int MaximumServerInstances = 8;
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint PipeRejectRemoteClients = 0x00000008;
    private const int BufferSize = 16_384;

    public NamedPipeServerStream Create()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddRule(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl);
        AddRule(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl);
        if (securityOptions.ApiClientSid is { } apiSid)
        {
            AddRule(
                security,
                new SecurityIdentifier(apiSid),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize);
        }
        if (securityOptions.LauncherClientSid is { } launcherSid)
        {
            AddRule(
                security,
                new SecurityIdentifier(launcherSid),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize);
        }

        var descriptor = security.GetSecurityDescriptorBinaryForm();
        var pinnedDescriptor = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = pinnedDescriptor.AddrOfPinnedObject(),
                InheritHandle = false
            };
            var handle = CreateNamedPipe(
                $@"\\.\pipe\{ContractVersions.ManagerPipeName}",
                PipeAccessDuplex | FileFlagOverlapped,
                PipeRejectRemoteClients,
                MaximumServerInstances,
                BufferSize,
                BufferSize,
                defaultTimeout: 15_000,
                ref attributes);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "Não foi possível criar o canal local do Manager.");
            }

            try
            {
                return new NamedPipeServerStream(
                    PipeDirection.InOut,
                    isAsync: true,
                    isConnected: false,
                    handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            pinnedDescriptor.Free();
        }
    }

    private static void AddRule(
        PipeSecurity security,
        SecurityIdentifier sid,
        PipeAccessRights rights) =>
        security.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal int Length;
        internal IntPtr SecurityDescriptor;

        [MarshalAs(UnmanagedType.Bool)]
        internal bool InheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern SafePipeHandle CreateNamedPipe(
        string pipeName,
        uint openMode,
        uint pipeMode,
        int maximumInstances,
        int outBufferSize,
        int inBufferSize,
        int defaultTimeout,
        ref SecurityAttributes securityAttributes);
}
