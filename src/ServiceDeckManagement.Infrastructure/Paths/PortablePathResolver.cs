using System.Buffers;
using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.Infrastructure.Paths;

/// <summary>
/// Resolve somente caminhos descendentes da raiz e bloqueia reparse points.
/// </summary>
public sealed class PortablePathResolver(IProductRootProvider rootProvider)
    : IPortablePathResolver
{
    private const int MaximumRelativePathLength = 1_024;
    private static readonly SearchValues<char> ForbiddenWindowsPathCharacters =
        SearchValues.Create("\"<>|:*?");

    public PathResolutionResult Resolve(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Length > MaximumRelativePathLength ||
            relativePath.Any(char.IsControl))
        {
            return PathResolutionResult.Failure(
                "path.required",
                "Informe um caminho relativo não vazio, sem controles e com até 1.024 caracteres.");
        }

        if (Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith("//", StringComparison.Ordinal) ||
            relativePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return PathResolutionResult.Failure(
                "path.rooted",
                "Caminhos absolutos, UNC ou de dispositivo não são permitidos.");
        }

        try
        {
            var root = Path.GetFullPath(rootProvider.RootPath).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            var relative = Path.GetRelativePath(root, fullPath);
            if (relative == ".." ||
                relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(relative))
            {
                return PathResolutionResult.Failure(
                    "path.outsideRoot",
                    "O caminho não pode sair da raiz portátil.");
            }

            if (ContainsInvalidWindowsSegment(relativePath))
            {
                return PathResolutionResult.Failure(
                    "path.segment",
                    "O caminho contém segmento ambíguo, reservado ou inválido no Windows.");
            }

            if (ContainsExistingReparsePoint(root, relative))
            {
                return PathResolutionResult.Failure(
                    "path.reparsePoint",
                    "O caminho não pode atravessar junction, symlink ou reparse point.");
            }

            return PathResolutionResult.Success(fullPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  IOException or
                  NotSupportedException or
                  UnauthorizedAccessException)
        {
            return PathResolutionResult.Failure(
                "path.invalid",
                "O caminho relativo é inválido ou inacessível.");
        }
    }

    private static bool ContainsExistingReparsePoint(
        string root,
        string relativePath)
    {
        if (HasReparsePoint(root))
        {
            return true;
        }

        var current = root;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                HasReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool ContainsInvalidWindowsSegment(string relativePath)
    {
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(segment) ||
                segment is "." or ".." ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                segment.AsSpan().IndexOfAny(ForbiddenWindowsPathCharacters) >= 0)
            {
                return true;
            }

            var deviceName = segment.Split('.', 2)[0];
            if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                IsNumberedDevice(deviceName, "COM") ||
                IsNumberedDevice(deviceName, "LPT"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumberedDevice(string value, string prefix) =>
        value.Length == 4 &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        value[3] is >= '1' and <= '9';
}
