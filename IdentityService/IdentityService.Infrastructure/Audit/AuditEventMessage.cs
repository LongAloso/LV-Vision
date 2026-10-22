using System.Text.Json.Serialization;

namespace IdentityService.Infrastructure.Audit;

internal sealed class AuditEventMessage
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("occurredAtUtc")]
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; init; } = default!;

    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "Information";

    [JsonPropertyName("action")]
    public string Action { get; init; } = default!;

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; init; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; init; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; init; } = new();
}
