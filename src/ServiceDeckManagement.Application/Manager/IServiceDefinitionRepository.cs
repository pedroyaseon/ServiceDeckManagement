using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Application.Manager;

public interface IServiceDefinitionRepository
{
    Task<IReadOnlyList<ServiceDefinitionV1>> ListAsync(CancellationToken cancellationToken);

    Task<ServiceDefinitionV1?> FindAsync(string serviceId, CancellationToken cancellationToken);

    Task SaveAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken);

    Task DeleteAsync(string serviceId, CancellationToken cancellationToken);
}
