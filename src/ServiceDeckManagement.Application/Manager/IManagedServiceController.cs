using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Application.Manager;

public interface IManagedServiceController
{
    Task<ManagedServiceRegistration> InspectAsync(
        ServiceDefinitionV1 definition,
        CancellationToken cancellationToken);

    Task InstallAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task UpdateAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task RemoveAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task StartAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task StopAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task RestartAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task RepairAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);
}
