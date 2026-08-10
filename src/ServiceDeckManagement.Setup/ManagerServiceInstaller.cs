using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Infrastructure.WindowsServices;

namespace ServiceDeckManagement.Setup;

public sealed class ManagerServiceInstaller(
    ProductPaths paths,
    IWindowsServiceControlBackend services,
    IManagerSetupSecurity securityProvisioner)
{
    public const string ServiceName = "ServiceDeckManagement.Manager";
    public const string OwnershipMarker = "ServiceDeckManagement:manager:v1";
    private const uint AutomaticStart = 2;
    private static readonly TimeSpan TransitionTimeout = TimeSpan.FromSeconds(20);

    public async Task InstallOrRepairAsync(
        string launcherSid,
        CancellationToken cancellationToken)
    {
        var normalized = ManagerSecurityOptionsValidator.NormalizeAndValidate(
            new(ApiClientSid: null, LauncherClientSid: launcherSid));
        ValidatePortableLayout();
        var existing = services.Query(ServiceName);
        if (existing is not null &&
            !string.Equals(existing.Description, OwnershipMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "O serviço existente não pertence ao Service Deck Management.");
        }

        var previous = securityProvisioner.Load();
        var security = ManagerSecurityOptionsValidator.NormalizeAndValidate(
            new(previous.ApiClientSid, normalized.LauncherClientSid));
        securityProvisioner.SaveAndHarden(security);

        var expected = CreateExpectedRecord();
        if (existing is null)
        {
            services.Create(expected);
        }
        else
        {
            if (existing.State != ManagedServiceState.Stopped)
            {
                await services.StopAsync(
                    ServiceName, TransitionTimeout, cancellationToken).ConfigureAwait(false);
            }

            services.Update(expected);
        }

        await services.StartAsync(
            ServiceName, TransitionTimeout, cancellationToken).ConfigureAwait(false);
    }

    public WindowsServiceRecord CreateExpectedRecord() => new(
        ServiceName,
        "Service Deck Management Manager",
        QuoteExecutable(paths.ManagerExecutable),
        OwnershipMarker,
        AutomaticStart,
        ManagedServiceState.Stopped,
        ProcessId: null);

    private void ValidatePortableLayout()
    {
        EnsureRegularDirectory(paths.Application);
        EnsureRegularExecutable(paths.ManagerExecutable);
        EnsureRegularExecutable(paths.SetupExecutable);
    }

    private static void EnsureRegularDirectory(string path)
    {
        if (!Directory.Exists(path) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("A pasta de binários do produto é inválida.");
        }
    }

    private static void EnsureRegularExecutable(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Um binário obrigatório não foi encontrado.");
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
            !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Um binário obrigatório é inválido.");
        }
    }

    private static string QuoteExecutable(string path)
    {
        if (path.Contains('"', StringComparison.Ordinal))
        {
            throw new InvalidDataException("O caminho do Manager contém caractere inválido.");
        }

        return $"\"{Path.GetFullPath(path)}\"";
    }
}

public interface IManagerSetupSecurity
{
    ManagerSecurityOptions Load();

    void SaveAndHarden(ManagerSecurityOptions options);
}

public sealed class ManagerSetupSecurity(
    ManagerSecurityConfigurationLoader loader,
    ManagerSecurityConfigurationWriter writer,
    PortableAclHardener hardener) : IManagerSetupSecurity
{
    public ManagerSecurityOptions Load() => loader.Load();

    public void SaveAndHarden(ManagerSecurityOptions options)
    {
        hardener.Apply(options);
        writer.Save(options);
    }
}
