namespace ServiceDeckManagement.Infrastructure.Security;

public sealed record ManagerSecurityOptions(string? ApiClientSid, string? LauncherClientSid)
{
    public static ManagerSecurityOptions LocalAdministratorsOnly { get; } = new(null, null);
}
