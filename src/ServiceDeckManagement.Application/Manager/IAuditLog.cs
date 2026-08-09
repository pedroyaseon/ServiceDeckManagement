namespace ServiceDeckManagement.Application.Manager;

public interface IAuditLog
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
