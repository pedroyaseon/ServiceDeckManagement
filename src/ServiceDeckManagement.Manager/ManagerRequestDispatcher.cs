using System.Text.Json;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Contracts.Versioning;
using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerRequestDispatcher(
    ServiceManagerCoordinator coordinator,
    IServiceLogReader logReader)
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
            !TryResolveActor(request, identity, out var actor, out var role) ||
            !ManagerAuthorization.IsAllowed(role, request.Operation))
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
                        ProcessId = item.ProcessId,
                        Executable = item.Executable,
                        WorkingDirectory = item.WorkingDirectory
                    }), ManagerJson.Options),
                ManagerOperationsV1.Details => await GetDetailsAsync(
                    request, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Logs => await ReadLogsAsync(request, cancellationToken)
                    .ConfigureAwait(false),
                ManagerOperationsV1.Create => await CreateAsync(
                    request, actor, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Update => await UpdateAsync(
                    request, actor, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Remove => await ServiceActionAsync(
                    request, actor, coordinator.RemoveAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Start => await ServiceActionAsync(
                    request, actor, coordinator.StartAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Stop => await ServiceActionAsync(
                    request, actor, coordinator.StopAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Restart => await ServiceActionAsync(
                    request, actor, coordinator.RestartAsync, cancellationToken).ConfigureAwait(false),
                ManagerOperationsV1.Repair => await ServiceActionAsync(
                    request, actor, coordinator.RepairAsync, cancellationToken).ConfigureAwait(false),
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

    private static bool TryResolveActor(
        ManagerRequestV1 request,
        ManagerClientIdentity identity,
        out string actor,
        out ManagerRole role)
    {
        if (!identity.IsApiClient)
        {
            actor = identity.SecurityIdentifier;
            role = identity.Role;
            return true;
        }

        actor = request.ActorId;
        role = default;
        return Guid.TryParseExact(request.ActorId, "D", out _) &&
            Enum.TryParse(request.ActorRole, ignoreCase: true, out role) &&
            Enum.IsDefined(role);
    }

    private async Task<JsonElement?> ReadLogsAsync(
        ManagerRequestV1 request,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload.Deserialize<ServiceLogsPayloadV1>(ManagerJson.Options) ??
            throw new JsonException("A consulta de logs está vazia.");
        var entries = await logReader.ReadAsync(
            payload.ServiceId,
            payload.AfterSequence,
            payload.Limit,
            cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(entries, ManagerJson.Options);
    }

    private async Task<JsonElement?> GetDetailsAsync(
        ManagerRequestV1 request,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload.Deserialize<ServiceIdPayloadV1>(ManagerJson.Options) ??
            throw new JsonException("A consulta de detalhes está vazia.");
        var item = await coordinator.GetAsync(payload.ServiceId, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new ManagedServiceDetailsV1
        {
            Status = new()
            {
                ServiceId = item.Registration.ServiceId,
                DisplayName = item.Registration.DisplayName,
                State = item.Registration.State.ToString().ToLowerInvariant(),
                StartMode = item.Registration.StartMode,
                RegistrationMatches = item.Registration.IdentityMatches,
                ProcessId = item.Registration.ProcessId,
                Executable = item.Registration.Executable,
                WorkingDirectory = item.Registration.WorkingDirectory
            },
            Definition = item.Definition
        }, ManagerJson.Options);
    }

    private async Task<JsonElement?> CreateAsync(
        ManagerRequestV1 request,
        string actor,
        CancellationToken cancellationToken)
    {
        var definition = request.Payload.Deserialize<ServiceDefinitionV1>(ManagerJson.Options) ??
            throw new JsonException("A definição está vazia.");
        await coordinator.CreateAsync(
            definition, actor, request.RequestId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<JsonElement?> UpdateAsync(
        ManagerRequestV1 request,
        string actor,
        CancellationToken cancellationToken)
    {
        var definition = request.Payload.Deserialize<ServiceDefinitionV1>(ManagerJson.Options) ??
            throw new JsonException("A definição está vazia.");
        await coordinator.UpdateAsync(
            definition, actor, request.RequestId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static async Task<JsonElement?> ServiceActionAsync(
        ManagerRequestV1 request,
        string actor,
        Func<string, string, string, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload.Deserialize<ServiceIdPayloadV1>(ManagerJson.Options) ??
            throw new JsonException("O identificador está vazio.");
        await action(payload.ServiceId, actor, request.RequestId, cancellationToken)
            .ConfigureAwait(false);
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
