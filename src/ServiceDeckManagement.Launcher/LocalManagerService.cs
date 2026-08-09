using System.IO;
using System.Text.Json;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Infrastructure.LocalProtocol;

namespace ServiceDeckManagement.Launcher;

public sealed class LocalManagerException(string message) : Exception(message);

public sealed class LocalManagerService(IManagerClient client)
{
    private const string DirectActor = "launcher.local";
    private const string DirectRole = "administrator";

    public Task<IReadOnlyList<ManagedServiceSnapshotV1>> GetServicesAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ManagedServiceSnapshotV1>>(
            ManagerOperationsV1.Inventory, new { }, cancellationToken);

    public Task<ManagedServiceDetailsV1> GetServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendAsync<ManagedServiceDetailsV1>(
            ManagerOperationsV1.Details,
            new ServiceIdPayloadV1 { ServiceId = serviceId },
            cancellationToken);

    public Task<IReadOnlyList<ServiceLogEntryV1>> GetLogsAsync(
        string serviceId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ServiceLogEntryV1>>(
            ManagerOperationsV1.Logs,
            new ServiceLogsPayloadV1
            {
                ServiceId = serviceId,
                AfterSequence = afterSequence,
                Limit = limit
            },
            cancellationToken);

    public Task CreateServiceAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
        SendAsync(ManagerOperationsV1.Create, definition, cancellationToken);

    public Task UpdateServiceAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
        SendAsync(ManagerOperationsV1.Update, definition, cancellationToken);

    public Task RemoveServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendServiceActionAsync(ManagerOperationsV1.Remove, serviceId, cancellationToken);

    public Task StartServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendServiceActionAsync(ManagerOperationsV1.Start, serviceId, cancellationToken);

    public Task StopServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendServiceActionAsync(ManagerOperationsV1.Stop, serviceId, cancellationToken);

    public Task RestartServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendServiceActionAsync(ManagerOperationsV1.Restart, serviceId, cancellationToken);

    public Task RepairServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendServiceActionAsync(ManagerOperationsV1.Repair, serviceId, cancellationToken);

    private Task SendServiceActionAsync(string operation, string serviceId, CancellationToken cancellationToken) =>
        SendAsync(operation, new ServiceIdPayloadV1 { ServiceId = serviceId }, cancellationToken);

    private async Task<T> SendAsync<T>(string operation, object payload, CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(operation, payload, cancellationToken).ConfigureAwait(false);
        if (response.Data is not JsonElement data)
        {
            throw new InvalidDataException("O Manager retornou uma resposta vazia.");
        }

        try
        {
            return data.Deserialize<T>(ManagerJson.Options) ??
                throw new InvalidDataException("O Manager retornou uma resposta vazia.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("O Manager retornou dados incompatíveis.", exception);
        }
    }

    private async Task SendAsync(string operation, object payload, CancellationToken cancellationToken) =>
        _ = await SendCoreAsync(operation, payload, cancellationToken).ConfigureAwait(false);

    private async Task<ManagerResponseV1> SendCoreAsync(
        string operation,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(
            operation,
            payload,
            DirectActor,
            DirectRole,
            cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new LocalManagerException(
                response.ErrorCode == "request.denied"
                    ? "O usuário do Windows não está autorizado no Manager."
                    : "O Manager recusou a operação solicitada.");
        }

        return response;
    }
}
