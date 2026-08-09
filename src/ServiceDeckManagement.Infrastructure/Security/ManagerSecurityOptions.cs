namespace ServiceDeckManagement.Infrastructure.Security;

public sealed record ManagerSecurityOptions(string? ApiClientSid)
{
    public static ManagerSecurityOptions LocalAdministratorsOnly { get; } = new((string?)null);
}
