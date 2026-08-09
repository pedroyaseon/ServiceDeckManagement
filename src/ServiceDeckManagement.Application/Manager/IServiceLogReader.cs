using ServiceDeckManagement.Contracts.Manager;

namespace ServiceDeckManagement.Application.Manager;

public interface IServiceLogReader
{
    Task<IReadOnlyList<ServiceLogEntryV1>> ReadAsync(
        string serviceId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken);
}
