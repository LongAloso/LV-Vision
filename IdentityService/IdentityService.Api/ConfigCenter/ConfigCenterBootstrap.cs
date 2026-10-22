#nullable enable

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spc.Shared.ConfigCenter;

public sealed class ConfigCenterOptions
{
    public string BaseUrl { get; set; } = default!;
    public int TimeoutSeconds { get; set; } = 10;
    public string FailoverSnapshotPath { get; set; } = "config.snapshot.json";
    public string? InternalApiKey { get; set; }
}

public static class ConfigCenterBootstrap
{
    public static async Task ApplyAsync(WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;

        var options = cfg.GetSection("ConfigCenter").Get<ConfigCenterOptions>()
                     ?? throw new Exception("Missing ConfigCenter section in appsettings.json.");

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new Exception("ConfigCenter:BaseUrl is required.");

        var env = builder.Environment.EnvironmentName;
        var serviceName = NormalizeServiceName(builder.Environment.ApplicationName);

        var result = await LoadAsync(
            baseUrl: options.BaseUrl,
            service: serviceName,
            env: env,
            timeoutSeconds: options.TimeoutSeconds,
            snapshotPath: options.FailoverSnapshotPath,
            internalApiKey: options.InternalApiKey
        );

        // Inject remote config into the current configuration
        var memoryConfig = new Dictionary<string, string?>();
        foreach (var kv in result.Config)
            memoryConfig[kv.Key] = kv.Value;
        builder.Configuration.AddInMemoryCollection(memoryConfig);
        // Allow environment variables to override ConfigCenter values (useful for local dev)
        builder.Configuration.AddEnvironmentVariables();
    }

    private static string NormalizeServiceName(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            return "UnknownService";

        // Most projects are named like "MeasurementService.Api" or "ApiGateway.Api"
        if (applicationName.EndsWith(".Api", StringComparison.OrdinalIgnoreCase))
            return applicationName.Substring(0, applicationName.Length - 4);

        return applicationName;
    }

    private static async Task<RemoteConfigResult> LoadAsync(
        string baseUrl,
        string service,
        string env,
        int timeoutSeconds,
        string snapshotPath,
        string? internalApiKey)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        if (!string.IsNullOrWhiteSpace(internalApiKey))
            http.DefaultRequestHeaders.Add("X-ConfigCenter-Internal-Key", internalApiKey);

        var url =
            $"{baseUrl.TrimEnd('/')}/api/config/latest" +
            $"?service={Uri.EscapeDataString(service)}" +
            $"&env={Uri.EscapeDataString(env)}";

        try
        {
            var resp = await http.GetFromJsonAsync<RemoteConfigResponse>(url)
                       ?? throw new Exception("ConfigCenter returned null response.");

            // snapshot for failover restarts
            var json = JsonSerializer.Serialize(resp, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(snapshotPath, json);

            return new RemoteConfigResult(resp.Meta.Hash, resp.Config);
        }
        catch
        {
            // failover: load last snapshot
            if (File.Exists(snapshotPath))
            {
                var json = await File.ReadAllTextAsync(snapshotPath);
                var resp = JsonSerializer.Deserialize<RemoteConfigResponse>(json);
                if (resp?.Config != null)
                    return new RemoteConfigResult(resp.Meta.Hash, resp.Config);
            }

            throw;
        }
    }

    public sealed record RemoteConfigResult(string Hash, Dictionary<string, string> Config);

    public sealed class RemoteConfigResponse
    {
        public RemoteConfigMeta Meta { get; set; } = new();
        public Dictionary<string, string> Config { get; set; } = new();

        public sealed class RemoteConfigMeta
        {
            public string Service { get; set; } = "";
            public string Env { get; set; } = "";
            public long Version { get; set; }
            public string Hash { get; set; } = "";
            public DateTimeOffset GeneratedAt { get; set; }
        }
    }
}
