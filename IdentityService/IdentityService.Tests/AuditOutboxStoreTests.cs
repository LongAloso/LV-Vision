using FluentAssertions;
using IdentityService.Infrastructure.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityService.Tests;

public sealed class AuditOutboxStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"spc-audit-outbox-{Guid.NewGuid():N}");

    [Fact]
    public async Task PersistReadComplete_Should_RoundTripDurably()
    {
        var store = CreateStore();
        var message = new AuditEventMessage
        {
            EventId = "event-001",
            ServiceName = "IdentityService",
            Action = "identity.test",
            Message = "test"
        };

        var path = await store.PersistAsync(message, CancellationToken.None);

        File.Exists(path).Should().BeTrue();
        store.CountPending().Should().Be(1);
        (await store.ReadAsync(path, CancellationToken.None))!.EventId.Should().Be(message.EventId);

        store.Complete(path);
        store.CountPending().Should().Be(0);
    }

    [Fact]
    public async Task PersistSameEventTwice_Should_KeepSingleFile()
    {
        var store = CreateStore();
        var message = new AuditEventMessage
        {
            EventId = "event-duplicate",
            ServiceName = "IdentityService",
            Action = "identity.test",
            Message = "test"
        };

        await store.PersistAsync(message, CancellationToken.None);
        await store.PersistAsync(message, CancellationToken.None);

        store.CountPending().Should().Be(1);
    }

    private AuditOutboxStore CreateStore()
        => new(Options.Create(new RabbitMqAuditSettings { OutboxPath = _root }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
