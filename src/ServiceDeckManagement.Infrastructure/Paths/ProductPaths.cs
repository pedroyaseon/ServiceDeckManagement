using ServiceDeckManagement.Application.Abstractions;

namespace ServiceDeckManagement.Infrastructure.Paths;

/// <summary>
/// Caminhos operacionais derivados exclusivamente da raiz portátil.
/// </summary>
public sealed class ProductPaths(IProductRootProvider rootProvider)
{
    public string Root => rootProvider.RootPath;

    public string Application => Path.Combine(Root, "app");

    public string Configuration => Path.Combine(Root, "config");

    public string ServiceDefinitions => Path.Combine(Configuration, "services");

    public string Data => Path.Combine(Root, "data");

    public string Logs => Path.Combine(Root, "logs");

    public string Runtime => Path.Combine(Root, "runtime");
}
