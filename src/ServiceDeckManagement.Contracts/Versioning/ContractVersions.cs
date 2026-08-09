namespace ServiceDeckManagement.Contracts.Versioning;

/// <summary>
/// Versões públicas suportadas pela linha 1.0.
/// </summary>
public static class ContractVersions
{
    public const int ServiceDefinitionSchema = 1;
    public const int LocalProtocol = 1;
    public const string ManagerPipeName = "ServiceDeckManagement.Manager.v1";
    public const string ApiRoutePrefix = "/api/v1";
}
