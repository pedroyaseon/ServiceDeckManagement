using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerPipeFactory(ManagerSecurityOptions securityOptions)
{
    public const int MaximumServerInstances = 8;
    private const int PipeRejectRemoteClients = 0x00000008;

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

        var options = PipeOptions.Asynchronous | (PipeOptions)PipeRejectRemoteClients;
        return NamedPipeServerStreamAcl.Create(
            ContractVersions.ManagerPipeName,
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

    private static void AddRule(
        PipeSecurity security,
        SecurityIdentifier sid,
        PipeAccessRights rights) =>
        security.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));
}
