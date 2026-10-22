using IdentityService.Infrastructure.Audit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityService.Api.Health;

public sealed class AuditOutboxHealthCheck(
    RabbitMqAuditEventPublisher publisher,
    IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = await publisher.GetStatusAsync(cancellationToken);
        var maximumPending = Math.Max(1, configuration.GetValue("Audit:MaxPendingFiles", 10_000));

        if (!status.IsWritable)
            return HealthCheckResult.Unhealthy($"Audit outbox is not writable ({status.Error}).");

        if (status.PendingCount >= maximumPending)
            return HealthCheckResult.Unhealthy($"Audit outbox backlog reached {status.PendingCount} files.");

        if (!status.IsBrokerReachable)
        {
            return HealthCheckResult.Degraded(
                $"RabbitMQ is unavailable; {status.PendingCount} audit event(s) are buffered.");
        }

        return status.PendingCount == 0
            ? HealthCheckResult.Healthy("Audit outbox is empty and RabbitMQ is reachable.")
            : HealthCheckResult.Degraded(
                $"RabbitMQ is reachable; {status.PendingCount} audit event(s) await retry.");
    }
}
