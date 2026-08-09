using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Domain.Services;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.WindowsServices;

/// <summary>
/// Aplica as regras de pertencimento antes de qualquer alteração privilegiada.
/// </summary>
public sealed class WindowsScmServiceController(
    ProductPaths paths,
    IWindowsServiceControlBackend backend) : IManagedServiceController
{
    public const string ServiceNamePrefix = ManagedServiceNames.Prefix;
    public const string DescriptionPrefix = "ServiceDeckManagement:v1:";
    private static readonly TimeSpan TransitionTimeout = TimeSpan.FromSeconds(30);

    public Task<ManagedServiceRegistration> InspectAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = CreateExpected(definition);
        var existing = backend.Query(expected.ServiceName);
        return Task.FromResult(existing is null
            ? new ManagedServiceRegistration(
                definition.Id,
                definition.DisplayName,
                definition.StartMode,
                ManagedServiceState.Missing,
                Exists: false,
                IdentityMatches: false,
                ProcessId: null)
            : new ManagedServiceRegistration(
                definition.Id,
                existing.DisplayName,
                MapStartMode(existing.StartType),
                existing.State,
                Exists: true,
                IdentityMatches: IdentityMatches(existing, expected),
                existing.ProcessId));
    }

    public Task InstallAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = CreateExpected(definition);
        if (backend.Query(expected.ServiceName) is not null)
        {
            throw new InvalidOperationException("Já existe uma entrada com esse nome no SCM.");
        }

        EnsureHostExecutable(expected.BinaryPath);
        backend.Create(expected);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = CreateExpected(definition);
        RequireOwned(expected);
        EnsureHostExecutable(expected.BinaryPath);
        backend.Update(expected);
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        var expected = CreateExpected(definition);
        var existing = backend.Query(expected.ServiceName);
        if (existing is null)
        {
            return;
        }

        EnsureOwned(existing, expected);
        await backend.StopAsync(expected.ServiceName, TransitionTimeout, cancellationToken)
            .ConfigureAwait(false);
        backend.Delete(expected.ServiceName);
    }

    public async Task StartAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        var expected = CreateExpected(definition);
        RequireOwned(expected);
        await backend.StartAsync(expected.ServiceName, TransitionTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        var expected = CreateExpected(definition);
        RequireOwned(expected);
        await backend.StopAsync(expected.ServiceName, TransitionTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RestartAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken)
    {
        var expected = CreateExpected(definition);
        RequireOwned(expected);
        await backend.StopAsync(expected.ServiceName, TransitionTimeout, cancellationToken)
            .ConfigureAwait(false);
        await backend.StartAsync(expected.ServiceName, TransitionTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task RepairAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = CreateExpected(definition);
        var existing = backend.Query(expected.ServiceName);
        EnsureHostExecutable(expected.BinaryPath);
        if (existing is null)
        {
            backend.Create(expected);
        }
        else
        {
            EnsureOwned(existing, expected, compareMutableFields: false);
            backend.Update(expected);
        }

        return Task.CompletedTask;
    }

    private WindowsServiceRecord CreateExpected(ServiceDefinitionV1 definition)
    {
        var hostPath = Path.Combine(paths.Application, "ServiceDeckManagement.Host.exe");
        if (hostPath.Contains('"'))
        {
            throw new InvalidDataException("O caminho do produto contém aspas.");
        }

        return new(
            ManagedServiceNames.FromId(definition.Id),
            $"Service Deck Management — {definition.DisplayName}",
            $"\"{hostPath}\" --service-id {definition.Id}",
            DescriptionPrefix + definition.Id,
            MapStartType(definition.StartMode),
            ManagedServiceState.Stopped,
            null);
    }

    private void RequireOwned(WindowsServiceRecord expected)
    {
        var existing = backend.Query(expected.ServiceName) ??
            throw new KeyNotFoundException("O serviço não está registrado no SCM.");
        EnsureOwned(existing, expected);
    }

    private static void EnsureOwned(
        WindowsServiceRecord existing,
        WindowsServiceRecord expected,
        bool compareMutableFields = true)
    {
        if (!string.Equals(existing.ServiceName, expected.ServiceName, StringComparison.Ordinal) ||
            !string.Equals(existing.Description, expected.Description, StringComparison.Ordinal) ||
            (compareMutableFields && !string.Equals(
                existing.BinaryPath, expected.BinaryPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException(
                "A entrada do SCM não possui a identidade verificável do produto.");
        }
    }

    private static bool IdentityMatches(
        WindowsServiceRecord existing,
        WindowsServiceRecord expected) =>
        string.Equals(existing.ServiceName, expected.ServiceName, StringComparison.Ordinal) &&
        string.Equals(existing.Description, expected.Description, StringComparison.Ordinal) &&
        string.Equals(existing.BinaryPath, expected.BinaryPath, StringComparison.OrdinalIgnoreCase);

    private static uint MapStartType(string mode) => mode switch
    {
        "automatic" => 2,
        "manual" => 3,
        "disabled" => 4,
        _ => throw new InvalidDataException("O modo de inicialização é inválido.")
    };

    private static string MapStartMode(uint mode) => mode switch
    {
        2 => "automatic",
        3 => "manual",
        4 => "disabled",
        _ => "unknown"
    };

    private static void EnsureHostExecutable(string binaryPath)
    {
        var closingQuote = binaryPath.IndexOf('"', 1);
        var path = closingQuote > 1 ? binaryPath[1..closingQuote] : string.Empty;
        if (!File.Exists(path) ||
            (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new FileNotFoundException(
                "O executável publicado do Service Host não foi encontrado em app/.");
        }
    }
}
