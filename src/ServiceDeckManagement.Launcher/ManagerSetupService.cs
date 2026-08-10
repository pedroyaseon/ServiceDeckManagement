using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Launcher;

public interface IElevatedSetupRunner
{
    Task<int> RunAsync(
        string executable,
        string workingDirectory,
        string launcherSid,
        CancellationToken cancellationToken);
}

public interface ICurrentWindowsIdentity
{
    string GetUserSid();
}

public sealed class CurrentWindowsIdentity : ICurrentWindowsIdentity
{
    public string GetUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        return identity.User?.Value ??
            throw new InvalidOperationException("O usuário atual não possui um SID do Windows.");
    }
}

public sealed class ElevatedSetupRunner : IElevatedSetupRunner
{
    public async Task<int> RunAsync(
        string executable,
        string workingDirectory,
        string launcherSid,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("install-manager");
        startInfo.ArgumentList.Add("--launcher-sid");
        startInfo.ArgumentList.Add(launcherSid);
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("O Windows não iniciou o configurador local.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}

public sealed record ManagerSetupOutcome(bool Success, bool Cancelled, string Message);

public sealed class ManagerSetupService(
    ProductPaths paths,
    IElevatedSetupRunner runner,
    ICurrentWindowsIdentity identity)
{
    private const int UacCancelled = 1223;

    public bool IsPackageComplete =>
        IsRegularFile(paths.SetupExecutable) && IsRegularFile(paths.ManagerExecutable);

    public bool HasLocalConfiguration =>
        File.Exists(Path.Combine(paths.Configuration, "manager-security.json"));

    public async Task<ManagerSetupOutcome> InstallOrRepairAsync(
        CancellationToken cancellationToken)
    {
        if (!IsPackageComplete)
        {
            return new(false, false,
                "O pacote local não contém os binários necessários para configurar o Manager.");
        }

        try
        {
            var exitCode = await runner.RunAsync(
                paths.SetupExecutable,
                paths.Root,
                identity.GetUserSid(),
                cancellationToken).ConfigureAwait(false);
            return exitCode switch
            {
                0 => new(true, false, "Manager configurado. Aguardando o serviço local..."),
                2 => new(false, false, "O configurador recebeu uma solicitação inválida."),
                3 => new(false, false, "A configuração exige confirmação administrativa."),
                4 => new(false, false, "A raiz portátil ou os binários locais são inválidos."),
                5 => new(false, false, "O Windows recusou o acesso necessário à instalação."),
                6 => new(false, false, "O Windows não concluiu o registro ou início do Manager."),
                7 => new(false, true, "A configuração do Manager foi cancelada."),
                _ => new(false, false, "O configurador local terminou com uma falha desconhecida.")
            };
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == UacCancelled)
        {
            return new(false, true, "A confirmação administrativa foi cancelada.");
        }
    }

    private static bool IsRegularFile(string path)
    {
        if (!File.Exists(path)) return false;
        var attributes = File.GetAttributes(path);
        return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
    }
}
