using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Application.Manager;

public sealed record ManagedServiceRegistration(
    string ServiceId,
    string DisplayName,
    string StartMode,
    ManagedServiceState State,
    bool Exists,
    bool IdentityMatches,
    int? ProcessId,
    string Executable,
    string WorkingDirectory);
