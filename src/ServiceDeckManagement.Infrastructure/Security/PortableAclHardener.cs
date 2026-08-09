using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Security;

/// <summary>
/// Remove herança permissiva dos diretórios privilegiados do produto.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PortableAclHardener(ProductPaths paths)
{
    public void Apply()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ACLs do produto requerem Windows.");
        }

        Harden(paths.Configuration);
        Harden(paths.ServiceDefinitions);
        Harden(paths.Data);
        Harden(paths.ManagerData);
    }

    private static void Harden(string path)
    {
        Directory.CreateDirectory(path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Diretórios privilegiados não podem ser reparse points.");
        }

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFullControl(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddFullControl(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void AddFullControl(
        DirectorySecurity security,
        SecurityIdentifier sid) =>
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
}
