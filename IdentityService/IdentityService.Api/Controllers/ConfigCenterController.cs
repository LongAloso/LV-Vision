using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigCenterController : ControllerBase
{
    private const string InternalKeyHeaderName = "X-ConfigCenter-Internal-Key";

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigCenterController> _logger;

    public ConfigCenterController(
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<ConfigCenterController> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("latest")]
    public IActionResult GetLatest([FromQuery] string service, [FromQuery] string env = "Production")
    {
        if (string.IsNullOrWhiteSpace(service))
            return BadRequest("Missing query parameter: service");

        var authResult = ValidateInternalKey();
        if (authResult is not null)
            return authResult;

        try
        {
            var basePath = _env.ContentRootPath;
            var appPath = Path.Combine(basePath, "appsettings.json");
            var devPath = Path.Combine(basePath, "appsettings.Development.json");

            var appJson = ReadJsonFile(appPath);
            var devJson = System.IO.File.Exists(devPath) ? ReadJsonFile(devPath) : new JsonObject();

            var mergedRoot = env.Equals("Development", StringComparison.OrdinalIgnoreCase)
                ? DeepMerge(appJson, devJson)
                : Clone(appJson);

            var generalIdentity = GetGeneralIdentityBlock(mergedRoot);
            var serviceBlock = GetServiceBlock(mergedRoot, service);
            var merged = DeepMerge(generalIdentity, serviceBlock);
            EnsureSharedFeatureDefaults(merged);

            var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Flatten(merged, string.Empty, flat);

            var version = GetVersion(appPath, devPath, env);
            var hash = ComputeHash(flat);

            return Ok(new RemoteConfigResponse
            {
                Meta = new RemoteConfigMeta
                {
                    Service = service,
                    Env = env,
                    Version = version,
                    Hash = hash,
                    GeneratedAt = DateTimeOffset.Now
                },
                Config = flat
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConfigCenter/latest failed for service={service}, env={env}", service, env);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "IdentityService", time = DateTimeOffset.Now });

    private IActionResult? ValidateInternalKey()
    {
        var expectedKey = _configuration["ConfigCenter:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            if (_env.IsDevelopment())
                return null;

            _logger.LogCritical("ConfigCenter:InternalApiKey is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "ConfigCenter is not configured." });
        }

        var providedKey = Request.Headers[InternalKeyHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey) ||
            !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Invalid ConfigCenter credentials." });
        }

        return null;
    }

    private static JsonObject GetGeneralIdentityBlock(JsonObject root)
        => Clone(root["Center"] as JsonObject ?? new JsonObject());

    private static void EnsureSharedFeatureDefaults(JsonObject merged)
    {
        if (merged["Features"] is not JsonObject features)
        {
            features = new JsonObject();
            merged["Features"] = features;
        }

        if (!features.ContainsKey("LegacyShiftEnabled"))
            features["LegacyShiftEnabled"] = true;
    }

    private static JsonObject GetServiceBlock(JsonObject root, string service)
    {
        var servicesObj = root["Services"] as JsonObject;
        var svcObj = servicesObj?[service] as JsonObject;
        return Clone(svcObj ?? new JsonObject());
    }

    private static long GetVersion(string appPath, string devPath, string env)
    {
        var appTicks = System.IO.File.Exists(appPath) ? System.IO.File.GetLastWriteTimeUtc(appPath).Ticks : 0;
        var devTicks = System.IO.File.Exists(devPath) ? System.IO.File.GetLastWriteTimeUtc(devPath).Ticks : 0;
        return env.Equals("Development", StringComparison.OrdinalIgnoreCase) ? Math.Max(appTicks, devTicks) : appTicks;
    }

    private static string ComputeHash(Dictionary<string, string> flat)
    {
        var ordered = flat.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(kv => $"{kv.Key}={kv.Value}").ToArray();
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", ordered));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static JsonObject ReadJsonFile(string path)
    {
        var jsonText = System.IO.File.ReadAllText(path).Trim('\uFEFF', '\u200B');
        using var doc = JsonDocument.Parse(jsonText);
        return ToJsonObject(doc.RootElement);
    }

    private static void Flatten(JsonNode? node, string prefix, Dictionary<string, string> output)
    {
        if (node is null) return;
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (kv.Value is null) continue;
                    var childPrefix = string.IsNullOrEmpty(prefix) ? kv.Key : $"{prefix}:{kv.Key}";
                    Flatten(kv.Value, childPrefix, output);
                }
                return;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is null) continue;
                    Flatten(arr[i], $"{prefix}:{i}", output);
                }
                return;
            case JsonValue value:
                if (!string.IsNullOrEmpty(prefix)) output[prefix] = JsonValueToString(value);
                return;
            default:
                if (!string.IsNullOrEmpty(prefix)) output[prefix] = node.ToJsonString();
                return;
        }
    }

    private static string JsonValueToString(JsonValue value)
    {
        if (value.TryGetValue<string>(out var s)) return s ?? string.Empty;
        if (value.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        if (value.TryGetValue<int>(out var i)) return i.ToString();
        if (value.TryGetValue<long>(out var l)) return l.ToString();
        if (value.TryGetValue<decimal>(out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetValue<double>(out var db)) return db.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.ToJsonString().Trim('"');
    }

    private static JsonObject DeepMerge(JsonObject baseObj, JsonObject overrideObj)
    {
        var result = Clone(baseObj);
        foreach (var kv in overrideObj)
        {
            if (kv.Value is null)
            {
                result[kv.Key] = null;
                continue;
            }
            if (!result.ContainsKey(kv.Key) || result[kv.Key] is null)
            {
                result[kv.Key] = CloneNode(kv.Value);
                continue;
            }
            if (result[kv.Key] is JsonObject existingObj && kv.Value is JsonObject overrideChildObj)
            {
                result[kv.Key] = DeepMerge(existingObj, overrideChildObj);
                continue;
            }
            result[kv.Key] = CloneNode(kv.Value);
        }
        return result;
    }

    private static JsonObject ToJsonObject(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            ? (JsonObject?)JsonNode.Parse(element.GetRawText()) ?? new JsonObject()
            : new JsonObject();

    private static JsonObject Clone(JsonObject obj)
        => (JsonObject?)obj.DeepClone() ?? new JsonObject();

    private static JsonNode? CloneNode(JsonNode? node) => node?.DeepClone();

    public sealed class RemoteConfigResponse
    {
        public RemoteConfigMeta Meta { get; set; } = new();
        public Dictionary<string, string> Config { get; set; } = new();
    }

    public sealed class RemoteConfigMeta
    {
        public string Service { get; set; } = string.Empty;
        public string Env { get; set; } = string.Empty;
        public long Version { get; set; }
        public string Hash { get; set; } = string.Empty;
        public DateTimeOffset GeneratedAt { get; set; }
    }
}
