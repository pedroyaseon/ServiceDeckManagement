using System.ComponentModel;
using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Infrastructure.WindowsServices;

namespace ServiceDeckManagement.Setup;

public static class SetupProgram
{
    public static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() ||
            !ManagerSetupArguments.TryParse(arguments, out var request) ||
            request is null)
        {
            return 2;
        }

        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            return 3;
        }

        try
        {
            var root = ProductRootLocator.FromApplicationBaseDirectory();
            var paths = new ProductPaths(root);
            var installer = new ManagerServiceInstaller(
                paths,
                new NativeWindowsServiceControlBackend(),
                new ManagerSetupSecurity(
                    new ManagerSecurityConfigurationLoader(paths),
                    new ManagerSecurityConfigurationWriter(paths),
                    new PortableAclHardener(paths)));
            await installer.InstallOrRepairAsync(
                request.LauncherSid, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 7;
        }
        catch (Exception exception) when (exception is
            FileNotFoundException or
            InvalidDataException or
            ProductRootNotFoundException)
        {
            return 4;
        }
        catch (UnauthorizedAccessException)
        {
            return 5;
        }
        catch (Exception exception) when (exception is
            Win32Exception or
            IOException or
            TimeoutException)
        {
            return 6;
        }
    }
}
