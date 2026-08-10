using ServiceDeckManagement.Infrastructure.Security;

namespace ServiceDeckManagement.Setup;

public sealed record ManagerSetupRequest(string LauncherSid);

public static class ManagerSetupArguments
{
    public static bool TryParse(string[] arguments, out ManagerSetupRequest? request)
    {
        request = null;
        if (arguments.Length != 3 ||
            !string.Equals(arguments[0], "install-manager", StringComparison.Ordinal) ||
            !string.Equals(arguments[1], "--launcher-sid", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var options = ManagerSecurityOptionsValidator.NormalizeAndValidate(
                new(ApiClientSid: null, LauncherClientSid: arguments[2]));
            if (options.LauncherClientSid is null) return false;
            request = new(options.LauncherClientSid);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
