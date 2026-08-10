using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Security;

/// <summary>
/// Remove herança permissiva e concede aos clientes locais somente os recursos necessários.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PortableAclHardener(ProductPaths paths)
{
    public void Apply(ManagerSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        HardenApplicationDirectory(
            paths.Application,
            options.LauncherClientSid,
            options.ApiClientSid);
        HardenDirectory(paths.Configuration);
        HardenDirectory(paths.ServiceDefinitions);
        HardenDirectory(paths.Data, options.ApiClientSid, ApiDirectoryAccess.Traverse);
        HardenDirectory(paths.ManagerData, options.ApiClientSid, ApiDirectoryAccess.Traverse);
        HardenDirectory(paths.ApiData, options.ApiClientSid, ApiDirectoryAccess.Modify);
    }

    private static void HardenApplicationDirectory(
        string path,
        string? launcherSid,
        string? apiSid)
    {
        Directory.CreateDirectory(path);
        HardenApplicationDirectoryEntry(path, launcherSid, apiSid);
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         current, "*", SearchOption.TopDirectoryOnly))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        "A pasta de binários não pode conter reparse points.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    HardenApplicationDirectoryEntry(entry, launcherSid, apiSid);
                    pending.Push(entry);
                }
                else
                {
                    HardenApplicationFile(entry, launcherSid, apiSid);
                }
            }
        }
    }

    private static void HardenApplicationDirectoryEntry(
        string path,
        string? launcherSid,
        string? apiSid)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("A pasta de binários não pode ser um reparse point.");
        }

        var administrators = new SecurityIdentifier(
            WellKnownSidType.BuiltinAdministratorsSid, null);
        var security = new DirectorySecurity();
        security.SetOwner(administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddDirectoryRule(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inherited: true);
        AddDirectoryRule(security, administrators, FileSystemRights.FullControl, inherited: true);
        AddApplicationDirectoryReader(security, launcherSid);
        AddApplicationDirectoryReader(security, apiSid);
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void HardenApplicationFile(
        string path,
        string? launcherSid,
        string? apiSid)
    {
        var administrators = new SecurityIdentifier(
            WellKnownSidType.BuiltinAdministratorsSid, null);
        var security = new FileSecurity();
        security.SetOwner(administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFileRule(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl);
        AddFileRule(security, administrators, FileSystemRights.FullControl);
        AddApplicationFileReader(security, launcherSid);
        AddApplicationFileReader(security, apiSid);
        new FileInfo(path).SetAccessControl(security);
    }

    private static void AddApplicationDirectoryReader(
        DirectorySecurity security,
        string? sid)
    {
        if (sid is null) return;
        AddDirectoryRule(
            security,
            new SecurityIdentifier(sid),
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
            inherited: true);
    }

    private static void AddApplicationFileReader(FileSecurity security, string? sid)
    {
        if (sid is null) return;
        AddFileRule(
            security,
            new SecurityIdentifier(sid),
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize);
    }

    public void ProtectTransportKey(ManagerSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(paths.ManagerTransportKey))
        {
            throw new FileNotFoundException("A chave de transporte ainda não existe.");
        }

        var attributes = File.GetAttributes(paths.ManagerTransportKey);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A chave de transporte deve ser um arquivo regular.");
        }

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFileRule(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl);
        AddFileRule(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl);
        if (options.ApiClientSid is { } sidText)
        {
            AddFileRule(
                security,
                new SecurityIdentifier(sidText),
                FileSystemRights.Read);
        }
        if (options.LauncherClientSid is { } launcherSid)
        {
            AddFileRule(
                security,
                new SecurityIdentifier(launcherSid),
                FileSystemRights.Read);
        }

        new FileInfo(paths.ManagerTransportKey).SetAccessControl(security);
    }

    private static void HardenDirectory(
        string path,
        string? apiSid = null,
        ApiDirectoryAccess apiAccess = ApiDirectoryAccess.None)
    {
        Directory.CreateDirectory(path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Diretórios privilegiados não podem ser reparse points.");
        }

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddDirectoryRule(
            security,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inherited: true);
        AddDirectoryRule(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            inherited: true);
        if (apiSid is not null && apiAccess != ApiDirectoryAccess.None)
        {
            var rights = apiAccess == ApiDirectoryAccess.Modify
                ? FileSystemRights.Modify | FileSystemRights.Synchronize
                : FileSystemRights.Traverse |
                  FileSystemRights.ReadAttributes |
                  FileSystemRights.ReadExtendedAttributes |
                  FileSystemRights.ReadPermissions;
            AddDirectoryRule(
                security,
                new SecurityIdentifier(apiSid),
                rights,
                inherited: apiAccess == ApiDirectoryAccess.Modify);
        }

        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void AddDirectoryRule(
        DirectorySecurity security,
        SecurityIdentifier sid,
        FileSystemRights rights,
        bool inherited) =>
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            rights,
            inherited ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit :
                InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow));

    private static void AddFileRule(
        FileSecurity security,
        SecurityIdentifier sid,
        FileSystemRights rights) =>
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            rights,
            AccessControlType.Allow));

    private enum ApiDirectoryAccess
    {
        None,
        Traverse,
        Modify
    }
}
