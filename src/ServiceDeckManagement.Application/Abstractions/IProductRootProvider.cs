namespace ServiceDeckManagement.Application.Abstractions;

/// <summary>
/// Fornece a raiz portátil e validada da instalação.
/// </summary>
public interface IProductRootProvider
{
    string RootPath { get; }
}
