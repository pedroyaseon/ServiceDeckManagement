namespace ServiceDeckManagement.Application.Manager;

public sealed record AuditEvent(
    DateTimeOffset TimestampUtc,
    string Actor,
    string Operation,
    string? ServiceId,
    bool Succeeded,
    string ResultCode,
    string CorrelationId);
