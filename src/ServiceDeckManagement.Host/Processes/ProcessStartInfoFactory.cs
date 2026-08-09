using System.Diagnostics;
using System.Text;
using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.Host.Processes;

/// <summary>
/// Constrói inicialização direta, sem shell e com argumentos separados.
/// </summary>
public sealed class ProcessStartInfoFactory(
    IPortablePathResolver pathResolver)
{
    public ProcessStartInfo Create(ResolvedServiceDefinition service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var executablePath = ResolveAgain(service.Definition.Executable);
        var workingDirectoryPath = ResolveAgain(service.Definition.WorkingDirectory);
        EnsureLaunchTargets(executablePath, workingDirectoryPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false),
            StandardErrorEncoding = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false)
        };

        foreach (var argument in service.Definition.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in service.Definition.Environment)
        {
            startInfo.Environment[name] = value;
        }

        startInfo.Environment["NO_COLOR"] = "1";
        startInfo.Environment["TERM"] = "dumb";

        return startInfo;
    }

    private string ResolveAgain(string relativePath)
    {
        var result = pathResolver.Resolve(relativePath);
        if (!result.IsValid || string.IsNullOrWhiteSpace(result.FullPath))
        {
            throw new InvalidDataException(
                result.ErrorMessage ?? "O caminho do processo é inválido.");
        }

        return result.FullPath;
    }

    private static void EnsureLaunchTargets(
        string executablePath,
        string workingDirectoryPath)
    {
        if (!File.Exists(executablePath) ||
            (File.GetAttributes(executablePath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new FileNotFoundException(
                "O executável gerenciado não está disponível para inicialização.");
        }

        if (!Directory.Exists(workingDirectoryPath) ||
            (File.GetAttributes(workingDirectoryPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new DirectoryNotFoundException(
                "O diretório de trabalho não está disponível para inicialização.");
        }
    }
}
