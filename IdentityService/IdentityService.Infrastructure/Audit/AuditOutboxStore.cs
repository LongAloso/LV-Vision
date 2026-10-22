using System.Text.Json;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Audit;

internal sealed class AuditOutboxStore
{
    private readonly string _rootPath;
    private readonly string _quarantinePath;

    public AuditOutboxStore(IOptions<RabbitMqAuditSettings> options)
    {
        var configuredPath = options.Value.OutboxPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException("Audit:OutboxPath is required.");

        _rootPath = Path.GetFullPath(configuredPath, Directory.GetCurrentDirectory());
        _quarantinePath = Path.Combine(_rootPath, ".invalid");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> PersistAsync(AuditEventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var finalPath = GetEventPath(message.EventId);
        if (File.Exists(finalPath))
            return finalPath;

        var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, message, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(temporaryPath, finalPath);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                // The event id is the idempotency key. Another concurrent call
                // already persisted the same event, so the durable result exists.
            }

            return finalPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public IEnumerable<string> EnumeratePending(int maximumCount)
        => Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(Math.Max(1, maximumCount));

    public async Task<AuditEventMessage?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<AuditEventMessage>(
            stream,
            cancellationToken: cancellationToken);
    }

    public void Complete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Quarantine(string path)
    {
        if (!File.Exists(path))
            return;

        Directory.CreateDirectory(_quarantinePath);
        var destination = Path.Combine(
            _quarantinePath,
            $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.invalid.json");
        File.Move(path, destination, overwrite: false);
    }

    public int CountPending()
        => Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly).Count();

    public bool TryProbeWritable(out string? error)
    {
        var probePath = Path.Combine(_rootPath, $".probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.WriteThrough))
            {
                stream.WriteByte(1);
                stream.Flush(flushToDisk: true);
            }

            File.Delete(probePath);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.GetType().Name;
            return false;
        }
        finally
        {
            if (File.Exists(probePath))
                File.Delete(probePath);
        }
    }

    private string GetEventPath(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) ||
            eventId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidOperationException("Audit event id contains invalid file-name characters.");
        }

        return Path.Combine(_rootPath, $"{eventId}.json");
    }
}
