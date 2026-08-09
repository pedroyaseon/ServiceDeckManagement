using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ServiceDeckManagement.Manager;

public static class ManagerPipeFactory
{
    public const string PipeName = "ServiceDeckManagement.Manager.v1";
    public const int MaximumServerInstances = 8;
    private const int PipeRejectRemoteClients = 0x00000008;

    public static NamedPipeServerStream Create()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFullControl(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddFullControl(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

        var options = PipeOptions.Asynchronous | (PipeOptions)PipeRejectRemoteClients;
        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            MaximumServerInstances,
            PipeTransmissionMode.Byte,
            options,
            inBufferSize: 16_384,
            outBufferSize: 16_384,
            security,
            HandleInheritability.None,
            PipeAccessRights.ReadWrite);
    }

    private static void AddFullControl(PipeSecurity security, SecurityIdentifier sid) =>
        security.AddAccessRule(new PipeAccessRule(
            sid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
}
