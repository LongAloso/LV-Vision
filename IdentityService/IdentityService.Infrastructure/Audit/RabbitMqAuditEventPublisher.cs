using System.Text;
using System.Text.Json;
using System.Diagnostics;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace IdentityService.Infrastructure.Audit;

public sealed class RabbitMqAuditSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "audit_events";
    public string ServiceName { get; set; } = "IdentityService";
    public string? Environment { get; set; }
    public string OutboxPath { get; set; } = "data/audit-outbox";
    public int FlushIntervalSeconds { get; set; } = 5;
    public int FlushBatchSize { get; set; } = 100;
    public int MaxPendingFiles { get; set; } = 10_000;
    public int PublishConfirmTimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// P3.4: Publish audit events to RabbitMQ. Connections are lazy and shared.
/// Failures are logged but never thrown, so business flow is unaffected if
/// the broker is briefly unreachable.
/// </summary>
public sealed class RabbitMqAuditEventPublisher : IAuditEventPublisher, IHostedService, IDisposable
{
    private readonly RabbitMqAuditSettings _settings;
    private readonly AuditOutboxStore _outbox;
    private readonly ILogger<RabbitMqAuditEventPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IModel? _channel;
    private CancellationTokenSource? _pumpCancellation;
    private Task? _pumpTask;
    private bool _disposed;

    internal RabbitMqAuditEventPublisher(
        IOptions<RabbitMqAuditSettings> options,
        AuditOutboxStore outbox,
        ILogger<RabbitMqAuditEventPublisher> logger)
    {
        _settings = options.Value;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task PublishAsync(
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
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? outboxPath = null;
        try
        {
            var payload = new AuditEventMessage
            {
                ServiceName = _settings.ServiceName,
                Environment = _settings.Environment,
                UserId = userId,
                UserName = userName,
                Level = level,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Message = message,
                CorrelationId = Activity.Current?.TraceId.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Data = data ?? new Dictionary<string, object>()
            };

            // Persist before attempting RabbitMQ. Once this returns, a broker or
            // process crash cannot lose the event; the background pump retries it.
            outboxPath = await _outbox.PersistAsync(payload, cancellationToken);

            await _gate.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    PublishAndConfirm(payload);
                    _outbox.Complete(outboxPath);
                }
                catch
                {
                    // Reset while holding the gate so another publisher cannot
                    // start using the same channel between failure and cleanup.
                    ResetChannel();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                outboxPath is null
                    ? "Failed to persist audit event {Action}; audit readiness will fail."
                    : "Failed to publish audit event {Action}; durable outbox will retry.",
                action);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = RunPumpAsync(_pumpCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_pumpCancellation is null || _pumpTask is null)
            return;

        await _pumpCancellation.CancelAsync();
        try
        {
            await _pumpTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal host shutdown.
        }
    }

    public async Task<AuditOutboxStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_outbox.TryProbeWritable(out var writeError))
            return new AuditOutboxStatus(false, false, _outbox.CountPending(), writeError);

        var pending = _outbox.CountPending();
        var brokerReachable = true;
        string? error = null;
        try
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    EnsureChannel();
                }
                catch
                {
                    ResetChannel();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            brokerReachable = false;
            error = ex.GetType().Name;
        }

        return new AuditOutboxStatus(true, brokerReachable, pending, error);
    }

    private async Task RunPumpAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(_settings.FlushIntervalSeconds, 1, 300));
        using var timer = new PeriodicTimer(interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            await FlushPendingAsync(cancellationToken);
            if (!await timer.WaitForNextTickAsync(cancellationToken))
                break;
        }
    }

    private async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        foreach (var path in _outbox.EnumeratePending(Math.Clamp(_settings.FlushBatchSize, 1, 1_000)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuditEventMessage? message;
            try
            {
                message = await _outbox.ReadAsync(path, cancellationToken);
                if (message is null)
                    throw new InvalidDataException("Audit outbox file contains no event.");
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            {
                _logger.LogError(ex, "Quarantining invalid audit outbox file {FileName}.", Path.GetFileName(path));
                _outbox.Quarantine(path);
                continue;
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                PublishAndConfirm(message);
                _outbox.Complete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit outbox flush paused; RabbitMQ is unavailable.");
                ResetChannel();
                break;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private void PublishAndConfirm(AuditEventMessage payload)
    {
        EnsureChannel();
        var channel = _channel ?? throw new InvalidOperationException("RabbitMQ audit channel is unavailable.");

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";
        props.ContentEncoding = "utf-8";
        props.Type = "spc.audit.v1";
        props.MessageId = payload.EventId;
        props.CorrelationId = payload.CorrelationId;
        props.Timestamp = new AmqpTimestamp(payload.OccurredAtUtc.ToUnixTimeSeconds());

        channel.BasicPublish(_settings.Exchange, routingKey: payload.Action, basicProperties: props, body: body);
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(
            Math.Clamp(_settings.PublishConfirmTimeoutSeconds, 1, 60)));
    }

    private void EnsureChannel()
    {
        if (_disposed) return;
        if (_channel is { IsOpen: true }) return;

        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connection?.Dispose();
        _connection = factory.CreateConnection("identityservice-audit");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_settings.Exchange, ExchangeType.Topic, durable: true);
        _channel.ConfirmSelect();
    }

    private void ResetChannel()
    {
        try { _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Dispose(); } catch { /* ignore */ }
        _channel = null;
        _connection = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _pumpCancellation?.Cancel();
        _pumpCancellation?.Dispose();
        ResetChannel();
        _gate.Dispose();
    }
}

public sealed record AuditOutboxStatus(
    bool IsWritable,
    bool IsBrokerReachable,
    int PendingCount,
    string? Error);
