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

    public string ManagerData => Path.Combine(Data, "manager");

    public string ManagerAudit => Path.Combine(ManagerData, "audit-v1.jsonl");

    public string ManagerTransportKey => Path.Combine(ManagerData, "transport-key.bin");

    public string ApiData => Path.Combine(Data, "api");

    public string ApiDatabase => Path.Combine(ApiData, "servicedeckmanagement.db");

    public string ApiProtectionKeys => Path.Combine(ApiData, "protection-keys");

    public string ApiLogs => Path.Combine(Logs, "api");
}
