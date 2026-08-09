using System.Text.Json;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerRequestDispatcher(ServiceManagerCoordinator coordinator)
{
    public async Task<ManagerResponseV1> DispatchAsync(
        ManagerRequestV1 request,
        ManagerClientIdentity identity,
        CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != ContractVersions.LocalProtocol)
        {
            return Failure(request.RequestId, "protocol.unsupported",
                "A versão do protocolo local não é suportada.");
        }

        if (!Guid.TryParseExact(request.RequestId, "D", out _) ||
            !ManagerAuthorization.IsAllowed(identity.Role, request.Operation))
        {
            return Failure(request.RequestId, "request.denied",
                "A requisição não foi autorizada.");
        }

        try
        {
            JsonElement? data = request.Operation switch
            {
                ManagerOperationsV1.Ping => JsonSerializer.SerializeToElement(
                    new { status = "ok" }, ManagerJson.Options),
                ManagerOperationsV1.Inventory => JsonSerializer.SerializeToElement(
                    (await coordinator.ListAsync(cancellationToken).ConfigureAwait(false))
                    .Select(item => new ManagedServiceSnapshotV1
                    {
                        ServiceId = item.ServiceId,
                        DisplayName = item.DisplayName,
                        State = item.State.ToString().ToLowerInvariant(),
                        StartMode = item.StartMode,
                        RegistrationMatches = item.IdentityMatches,
                        ProcessId = item.ProcessId
                    }), ManagerJson.Options),
                ManagerOperationsV1.Create => await CreateAsync(request, identity, cancellationToken)
                    .ConfigureAwait(false),
                ManagerOperationsV1.Update => await UpdateAsync(request, identity, cancellationToken)
                    .ConfigureAwait(false),
                ManagerOperationsV1.Remove => await ServiceActionAsync(
                    request, identity, coordinator.RemoveAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Start => await ServiceActionAsync(
                    request, identity, coordinator.StartAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Stop => await ServiceActionAsync(
                    request, identity, coordinator.StopAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Restart => await ServiceActionAsync(
                    request, identity, coordinator.RestartAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Repair => await ServiceActionAsync(
                    request, identity, coordinator.RepairAsync, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException("Operação desconhecida.")
            };

            return new()
            {
                ProtocolVersion = ContractVersions.LocalProtocol,
                RequestId = request.RequestId,
                Success = true,
                Data = data
            };
        }
        catch (Exception exception) when (exception is
            InvalidDataException or
            InvalidOperationException or
            KeyNotFoundException or
            UnauthorizedAccessException or
            IOException or
            System.ComponentModel.Win32Exception or
            TimeoutException or
            JsonException)
        {
            return Failure(request.RequestId, "operation.failed",
                "A operação foi recusada ou não pôde ser concluída.");
        }
    }

    private async Task<JsonElement?> CreateAsync(
        ManagerRequestV1 request,
        ManagerClientIdentity identity,
        CancellationToken cancellationToken)
    {
        var definition = request.Payload.Deserialize<ServiceDefinitionV1>(ManagerJson.Options) ??
            throw new JsonException("A definição está vazia.");
        await coordinator.CreateAsync(
            definition, identity.SecurityIdentifier, request.RequestId, cancellationToken)
            .ConfigureAwait(false);
        return null;
    }

    private async Task<JsonElement?> UpdateAsync(
        ManagerRequestV1 request,
        ManagerClientIdentity identity,
        CancellationToken cancellationToken)
    {
        var definition = request.Payload.Deserialize<ServiceDefinitionV1>(ManagerJson.Options) ??
            throw new JsonException("A definição está vazia.");
        await coordinator.UpdateAsync(
            definition, identity.SecurityIdentifier, request.RequestId, cancellationToken)
            .ConfigureAwait(false);
        return null;
    }

    private static async Task<JsonElement?> ServiceActionAsync(
        ManagerRequestV1 request,
        ManagerClientIdentity identity,
        Func<string, string, string, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload.Deserialize<ServiceIdPayloadV1>(ManagerJson.Options) ??
            throw new JsonException("O identificador está vazio.");
        await action(
            payload.ServiceId,
            identity.SecurityIdentifier,
            request.RequestId,
            cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static ManagerResponseV1 Failure(
        string requestId,
        string code,
        string message) => new()
        {
            ProtocolVersion = ContractVersions.LocalProtocol,
            RequestId = requestId,
            Success = false,
            ErrorCode = code,
            Message = message
        };
}
