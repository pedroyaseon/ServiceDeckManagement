using System.Runtime.Versioning;
using System.Security.Principal;

namespace ServiceDeckManagement.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public static class ManagerSecurityOptionsValidator
{
    public static ManagerSecurityOptions NormalizeAndValidate(ManagerSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var apiSid = ValidateSid(options.ApiClientSid, "API");
        var launcherSid = ValidateSid(options.LauncherClientSid, "Launcher");
        if (apiSid is not null &&
            string.Equals(apiSid, launcherSid, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("API e Launcher devem usar identidades Windows distintas.");
        }

        return new(apiSid, launcherSid);
    }

    private static string? ValidateSid(string? value, string client)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var sid = new SecurityIdentifier(value);
            if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                sid.IsWellKnown(WellKnownSidType.WorldSid) ||
                sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid) ||
                sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid))
            {
                throw new InvalidDataException(
                    $"O SID do {client} deve representar uma identidade dedicada.");
            }

            return sid.Value;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"O SID configurado para o {client} é inválido.", exception);
        }
    }
}
