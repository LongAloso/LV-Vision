using System.Diagnostics;
using ApiGateway.Api.Configuration;
using ApiGateway.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Spc.Shared.ConfigCenter;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// 0. Kestrel tuning — handle many concurrent WebSocket + HTTP connections
// =========================================================
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Nâng trần cho quy mô ~100 máy: mỗi browser có thể giữ nhiều WS (measurement/collection/notification hub)
    // + nhiều request HTTP đồng thời. Trần 500/100 cũ dễ chạm → request mới (gồm login) bị xếp hàng/treo.
    kestrel.Limits.MaxConcurrentConnections = 2000;
    kestrel.Limits.MaxConcurrentUpgradedConnections = 1000; // WebSocket slots
    kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(130);
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// =========================================================
// 0b. Logging
// =========================================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Có thể đổi sang Warning nếu muốn giảm log mạnh hơn
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning
);

// =========================================================
// 1. BOOTSTRAP CONFIG FROM ConfigCenter
// =========================================================
await ConfigCenterBootstrap.ApplyAsync(builder);

// =========================================================
// 2. Load Ocelot config
// =========================================================
var env = builder.Environment.EnvironmentName;
var reloadOcelotOnChange = builder.Environment.IsDevelopment();

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: reloadOcelotOnChange)
    .AddJsonFile($"ocelot.{env.ToLowerInvariant()}.json", optional: true, reloadOnChange: reloadOcelotOnChange)
    // Ocelot files are added after the default providers, so restore environment
    // precedence before applying friendly downstream-service overrides.
    .AddEnvironmentVariables();

DownstreamRouteOverrides.Apply(builder.Configuration);

// =========================================================
// 3. Services
// =========================================================
builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =========================================================
// 4. CORS
// =========================================================
// GatewayCors is local Gateway policy. Do not inherit legacy origins from
// ConfigCenter's old Q-OneSPC ApiGateway block.
var corsSection = builder.Configuration.GetSection("GatewayCors");
var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowCredentials = corsSection.GetValue("AllowCredentials", true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            // Safe default: không cho cross-site nếu chưa cấu hình
            policy.SetIsOriginAllowed(_ => false);
        }

        policy.AllowAnyHeader()
              .AllowAnyMethod();

        if (allowCredentials)
            policy.AllowCredentials();
        else
            policy.DisallowCredentials();
    });
});

// Nếu chạy sau reverse proxy / IIS / ARR / nginx thì nên bật forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.ForwardLimit = Math.Clamp(
        builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1),
        1,
        5);

    // TrustAll is intended only for local load simulation. Production supplies
    // an explicit internal proxy network through environment configuration.
    if (builder.Configuration.GetValue("ForwardedHeaders:TrustAll", false))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
    else
    {
        foreach (var cidr in builder.Configuration
                     .GetSection("ForwardedHeaders:KnownNetworks")
                     .Get<string[]>() ?? Array.Empty<string>())
        {
            var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !System.Net.IPAddress.TryParse(parts[0], out var prefix)
                || !int.TryParse(parts[1], out var prefixLength))
            {
                throw new InvalidOperationException($"Invalid ForwardedHeaders:KnownNetworks CIDR '{cidr}'.");
            }
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    }
});

// =========================================================
// 5. Build app
// =========================================================
var app = builder.Build();

app.UseResponseCompression();
app.UseForwardedHeaders();

// Ocelot forwards request headers, so replace any caller-provided chain with the
// client IP already validated by ForwardedHeadersMiddleware. Downstream services
// can then safely partition rate limits per original client.
app.Use(async (ctx, next) =>
{
    if (ctx.Connection.RemoteIpAddress is { } clientIp)
        ctx.Request.Headers["X-Forwarded-For"] = clientIp.ToString();
    await next();
});

// =========================================================
// 5a. HTTPS enforcement in Production (P1.1)
// =========================================================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// =========================================================
// 5b. Security Headers (P1.4) — apply to all responses
// =========================================================
app.UseMiddleware<SecurityHeadersMiddleware>();

// =========================================================
// 6. Request timing middleware
//    - Chỉ log request chậm
//    - Không spam log mọi request
// =========================================================
app.Use(async (ctx, next) =>
{
    var sw = Stopwatch.StartNew();

    await next();

    sw.Stop();

    // Bỏ qua health để đỡ noise
    if (ctx.Request.Path.StartsWithSegments("/health"))
        return;

    // Có thể chỉnh ngưỡng này
    if (sw.ElapsedMilliseconds >= 1000)
    {
        app.Logger.LogWarning(
            "Slow request {Method} {Path} => {StatusCode} in {ElapsedMs} ms",
            ctx.Request.Method,
            ctx.Request.Path,
            ctx.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
});

// =========================================================
// 7. HARD FIX cho CORS preflight với Ocelot
//    - Chỉ xử lý OPTIONS sớm
//    - Không log mỗi request như bản cũ
// =========================================================
app.Use(async (ctx, next) =>
{
    if (!HttpMethods.IsOptions(ctx.Request.Method))
    {
        await next();
        return;
    }

    var origin = ctx.Request.Headers.Origin.ToString();
    if (string.IsNullOrWhiteSpace(origin))
    {
        await next();
        return;
    }

    var isAllowedOrigin = allowedOrigins.Any(o =>
        string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));

    if (!isAllowedOrigin)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    var acrm = ctx.Request.Headers["Access-Control-Request-Method"].ToString();
    var acrh = ctx.Request.Headers["Access-Control-Request-Headers"].ToString();

    ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
    ctx.Response.Headers["Vary"] = "Origin";

    if (allowCredentials)
        ctx.Response.Headers["Access-Control-Allow-Credentials"] = "true";

    ctx.Response.Headers["Access-Control-Allow-Headers"] =
        !string.IsNullOrWhiteSpace(acrh)
            ? acrh
            : "Content-Type, Authorization, X-Requested-With";

    ctx.Response.Headers["Access-Control-Allow-Methods"] =
        !string.IsNullOrWhiteSpace(acrm)
            ? acrm
            : "GET,POST,PUT,DELETE,OPTIONS";

    ctx.Response.Headers["Access-Control-Max-Age"] = "86400";
    ctx.Response.StatusCode = StatusCodes.Status204NoContent;
});

// =========================================================
// 8. WebSockets
// =========================================================
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)  // was 120s — faster dead-connection detection
});

// =========================================================
// 9. Routing + Swagger
// =========================================================
app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// =========================================================
// 10. CORS phải chạy trước Ocelot
// =========================================================
app.UseCors("CorsPolicy");

// =========================================================
// 11. Internal endpoints
// =========================================================
app.Use(async (ctx, next) =>
{
    if (!HttpMethods.IsGet(ctx.Request.Method) ||
        !ctx.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    await Results.Ok(new
    {
        status = "OK",
        service = "ApiGateway",
        environment = app.Environment.EnvironmentName
    }).ExecuteAsync(ctx);
});

// ConfigCenter returns service runtime config and must not be reachable through
// the public gateway prefix. Services bootstrap from IdentityService directly.
app.Use(async (ctx, next) =>
{
    if (GatewayPathPolicy.IsInternalConfigPath(ctx.Request.Path))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

// =========================================================
// 12. Ocelot
// =========================================================
await app.UseOcelot();

app.Run();
