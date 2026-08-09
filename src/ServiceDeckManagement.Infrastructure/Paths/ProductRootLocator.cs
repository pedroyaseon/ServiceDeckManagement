using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.Infrastructure.Paths;

/// <summary>
/// Localiza a raiz por um marcador controlado pelo produto.
/// </summary>
public sealed class ProductRootLocator : IProductRootProvider
{
    public const string MarkerFileName = ".servicedeck-root";
    private const int MaximumParentDepth = 8;
    private const string ExpectedProductMarker = "product=ServiceDeckManagement";
    private readonly Lazy<string> rootPath;

    public ProductRootLocator(string startingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startingDirectory);
        rootPath = new Lazy<string>(
            () => Locate(startingDirectory),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string RootPath => rootPath.Value;

    public static ProductRootLocator FromApplicationBaseDirectory() =>
        new(AppContext.BaseDirectory);

    private static string Locate(string startingDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        for (var depth = 0;
             current is not null && depth <= MaximumParentDepth;
             depth++, current = current.Parent)
        {
            var markerPath = Path.Combine(current.FullName, MarkerFileName);
            if (!File.Exists(markerPath))
            {
                continue;
            }

            var markerLines = File.ReadAllLines(markerPath);
            if (!markerLines.Contains(
                    ExpectedProductMarker,
                    StringComparer.Ordinal))
            {
                throw new ProductRootNotFoundException(
                    "O marcador da raiz pertence a outro produto ou está corrompido.");
            }

            return current.FullName.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        throw new ProductRootNotFoundException(
            $"Não foi possível localizar {MarkerFileName} a partir do executável.");
    }
}
