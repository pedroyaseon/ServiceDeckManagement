using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Manager;

public sealed record ManagerClientIdentity(
    string SecurityIdentifier,
    ManagerRole Role,
    bool IsApiClient);
