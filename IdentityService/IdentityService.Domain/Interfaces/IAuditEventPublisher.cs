namespace IdentityService.Domain.Interfaces;

/// <summary>
/// P3.4: Publishes structured audit events. The audit pipeline is fire-and-forget;
/// callers must not depend on success to complete their business operation.
/// </summary>
public interface IAuditEventPublisher
{
    Task PublishAsync(
        string action,
        string message,
        string level = "Information",
        string? userId = null,
        string? userName = null,
        string? resourceType = null,
        string? resourceId = null,
        string? ipAddress = null,
        string? userAgent = null,
        Dictionary<string, object>? data = null,
        CancellationToken cancellationToken = default);
}
