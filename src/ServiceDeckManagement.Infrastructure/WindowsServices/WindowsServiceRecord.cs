using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Infrastructure.WindowsServices;

public sealed record WindowsServiceRecord(
    string ServiceName,
    string DisplayName,
    string BinaryPath,
    string Description,
    uint StartType,
    ManagedServiceState State,
    int? ProcessId);
