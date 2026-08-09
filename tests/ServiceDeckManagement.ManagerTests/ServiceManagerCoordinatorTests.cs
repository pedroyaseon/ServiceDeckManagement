using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.ManagerTests;

public sealed class ServiceManagerCoordinatorTests
{
    [Fact]
    public async Task Create_RollsBackDefinitionWhenScmInstallFails()
    {
        using var root = new TestProductRoot();
        var repository = new MemoryRepository();
        var services = new FailingManagedServiceController();
        var coordinator = CreateCoordinator(root, repository, services);

        await Assert.ThrowsAsync<IOException>(() => coordinator.CreateAsync(
            TestDefinitions.Create(),
            "S-1-5-18",
            Guid.NewGuid().ToString("D"),
            CancellationToken.None));

        Assert.Null(await repository.FindAsync("sample", CancellationToken.None));
    }

    [Fact]
    public async Task Create_RejectsSecretsUntilHostIntegrationExists()
    {
        using var root = new TestProductRoot();
        var repository = new MemoryRepository();
        var services = new FailingManagedServiceController();
        var coordinator = CreateCoordinator(root, repository, services);
        var definition = TestDefinitions.Create() with
        {
            SecretReferences = new(StringComparer.OrdinalIgnoreCase)
            {
                ["API_TOKEN"] = "secret-1"
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => coordinator.CreateAsync(
            definition,
            "S-1-5-18",
            Guid.NewGuid().ToString("D"),
            CancellationToken.None));

        Assert.Empty(repository.Items);
    }

    private static ServiceManagerCoordinator CreateCoordinator(
        TestProductRoot root,
        IServiceDefinitionRepository repository,
        IManagedServiceController services) => new(
            repository,
            services,
            new ServiceDefinitionValidator(new PortablePathResolver(root)),
            new NullAuditLog(),
            TimeProvider.System);

    private sealed class MemoryRepository : IServiceDefinitionRepository
    {
        internal Dictionary<string, ServiceDefinitionV1> Items { get; } =
            new(StringComparer.Ordinal);

        public Task<IReadOnlyList<ServiceDefinitionV1>> ListAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ServiceDefinitionV1>>(Items.Values.ToArray());
        }

        public Task<ServiceDefinitionV1?> FindAsync(
            string serviceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Items.GetValueOrDefault(serviceId));
        }

        public Task SaveAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items[definition.Id] = definition;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string serviceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items.Remove(serviceId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingManagedServiceController : IManagedServiceController
    {
        public Task<ManagedServiceRegistration> InspectAsync(
            ServiceDefinitionV1 definition,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task InstallAsync(
            ServiceDefinitionV1 definition,
            CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Falha simulada do SCM."));

        public Task UpdateAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StartAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StopAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RestartAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RepairAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NullAuditLog : IAuditLog
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
