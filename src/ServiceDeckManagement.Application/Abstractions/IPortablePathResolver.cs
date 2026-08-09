namespace ServiceDeckManagement.Application.Abstractions;

/// <summary>
/// Resolve caminhos relativos sem permitir saída da raiz portátil.
/// </summary>
public interface IPortablePathResolver
{
    PathResolutionResult Resolve(string? relativePath);
}

public sealed record PathResolutionResult(
    bool IsValid,
    string? FullPath,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static PathResolutionResult Success(string fullPath) =>
        new(true, fullPath, null, null);

    public static PathResolutionResult Failure(
        string errorCode,
        string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
