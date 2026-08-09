using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.Application.Manager;

/// <summary>
/// Coordena persistência e SCM sem permitir que estado parcial pareça válido.
/// </summary>
public sealed class ServiceManagerCoordinator(
    IServiceDefinitionRepository definitions,
    IManagedServiceController services,
    ServiceDefinitionValidator validator,
    IAuditLog auditLog,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ManagedServiceRegistration>> ListAsync(
        CancellationToken cancellationToken)
    {
        var items = await definitions.ListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ManagedServiceRegistration>(items.Count);
        foreach (var definition in items)
        {
            result.Add(await services.InspectAsync(definition, cancellationToken)
                .ConfigureAwait(false));
        }

        return result;
    }

    public async Task CreateAsync(
        ServiceDefinitionV1 definition,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(definition);
        if (await definitions.FindAsync(definition.Id, cancellationToken).ConfigureAwait(false)
            is not null)
        {
            throw new InvalidOperationException("O serviço já existe.");
        }

        await definitions.SaveAsync(definition, cancellationToken).ConfigureAwait(false);
        try
        {
            await services.InstallAsync(definition, cancellationToken).ConfigureAwait(false);
            await AuditAsync(actor, "service.create", definition.Id, true, "created", correlationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await definitions.DeleteAsync(definition.Id, CancellationToken.None).ConfigureAwait(false);
            await AuditAsync(actor, "service.create", definition.Id, false, "install_failed",
                correlationId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task UpdateAsync(
        ServiceDefinitionV1 definition,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(definition);
        var previous = await RequireAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        await definitions.SaveAsync(definition, cancellationToken).ConfigureAwait(false);
        try
        {
            await services.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);
            await AuditAsync(actor, "service.update", definition.Id, true, "updated", correlationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await definitions.SaveAsync(previous, CancellationToken.None).ConfigureAwait(false);
            await AuditAsync(actor, "service.update", definition.Id, false, "update_failed",
                correlationId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task RemoveAsync(
        string serviceId,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var definition = await RequireAsync(serviceId, cancellationToken).ConfigureAwait(false);
        await services.RemoveAsync(definition, cancellationToken).ConfigureAwait(false);
        await definitions.DeleteAsync(serviceId, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "service.remove", serviceId, true, "removed", correlationId,
            cancellationToken).ConfigureAwait(false);
    }

    public Task StartAsync(string serviceId, string actor, string correlationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(serviceId, actor, correlationId, "service.start", "started",
            services.StartAsync, cancellationToken);

    public Task StopAsync(string serviceId, string actor, string correlationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(serviceId, actor, correlationId, "service.stop", "stopped",
            services.StopAsync, cancellationToken);

    public Task RestartAsync(string serviceId, string actor, string correlationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(serviceId, actor, correlationId, "service.restart", "restarted",
            services.RestartAsync, cancellationToken);

    public Task RepairAsync(string serviceId, string actor, string correlationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(serviceId, actor, correlationId, "service.repair", "repaired",
            services.RepairAsync, cancellationToken);

    private async Task ExecuteAsync(
        string serviceId,
        string actor,
        string correlationId,
        string operation,
        string successCode,
        Func<ServiceDefinitionV1, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var definition = await RequireAsync(serviceId, cancellationToken).ConfigureAwait(false);
        try
        {
            await action(definition, cancellationToken).ConfigureAwait(false);
            await AuditAsync(actor, operation, serviceId, true, successCode, correlationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await AuditAsync(actor, operation, serviceId, false, "operation_failed", correlationId,
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<ServiceDefinitionV1> RequireAsync(
        string serviceId,
        CancellationToken cancellationToken)
    {
        if (!ServiceId.TryCreate(serviceId, out var canonical))
        {
            throw new InvalidDataException("O identificador do serviço é inválido.");
        }

        return await definitions.FindAsync(canonical.Value, cancellationToken).ConfigureAwait(false) ??
            throw new KeyNotFoundException("O serviço não foi encontrado.");
    }

    private void Validate(ServiceDefinitionV1 definition)
    {
        var result = validator.Validate(definition);
        if (!result.IsValid)
        {
            throw new InvalidDataException(string.Join(
                "; ",
                result.Errors.Select(item => $"{item.Code}:{item.Field}")));
        }


        if (definition.SecretReferences.Count > 0)
        {
            throw new InvalidDataException(
                "Referências de segredo permanecem bloqueadas até a integração segura com o Host.");
        }
    }

    private Task AuditAsync(
        string actor,
        string operation,
        string? serviceId,
        bool succeeded,
        string resultCode,
        string correlationId,
        CancellationToken cancellationToken) =>
        auditLog.AppendAsync(
            new(
                timeProvider.GetUtcNow(),
                actor,
                operation,
                serviceId,
                succeeded,
                resultCode,
                correlationId),
            cancellationToken);
}
