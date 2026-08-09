namespace ServiceDeckManagement.Domain.Services;

/// <summary>
/// Namespace estável compartilhado entre o registro no SCM e o Service Host.
/// </summary>
public static class ManagedServiceNames
{
    public const string Prefix = "ServiceDeckManagement.Managed.";

    public static string FromId(string serviceId) => Prefix + ServiceId.Create(serviceId).Value;
}
