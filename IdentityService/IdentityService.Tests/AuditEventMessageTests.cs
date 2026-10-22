using System.Text.Json;
using FluentAssertions;
using IdentityService.Infrastructure.Audit;
using Xunit;

namespace IdentityService.Tests;

public class AuditEventMessageTests
{
    [Fact]
    public void Serialize_Should_Use_Explicit_Web_Contract_And_Stable_Metadata()
    {
        var message = new AuditEventMessage
        {
            ServiceName = "IdentityService",
            Action = "User.Created",
            Message = "created",
            CorrelationId = "trace-id"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message));
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("serviceName").GetString().Should().Be("IdentityService");
        root.GetProperty("action").GetString().Should().Be("User.Created");
        root.GetProperty("correlationId").GetString().Should().Be("trace-id");
        root.TryGetProperty("ServiceName", out _).Should().BeFalse();
    }
}
