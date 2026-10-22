namespace ApiGateway.Api.Middleware;

/// <summary>
/// Adds security response headers to all responses.
/// Covers OWASP recommendations: X-Frame-Options, X-Content-Type-Options,
/// Referrer-Policy, Permissions-Policy, Content-Security-Policy, HSTS.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        // Set headers before response starts streaming. We attach to OnStarting
        // to be compatible with services that buffer responses (e.g., Ocelot).
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Clickjacking protection (checklist 3.9)
            if (!headers.ContainsKey("X-Frame-Options"))
                headers["X-Frame-Options"] = "DENY";

            // MIME-sniffing protection (checklist 3.5)
            if (!headers.ContainsKey("X-Content-Type-Options"))
                headers["X-Content-Type-Options"] = "nosniff";

            // Referrer leak protection
            if (!headers.ContainsKey("Referrer-Policy"))
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Disable powerful browser APIs by default
            if (!headers.ContainsKey("Permissions-Policy"))
                headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";

            // CSP — start with relaxed policy compatible with PrimeVue + SignalR.
            // TODO Phase 2: tighten by removing 'unsafe-inline' using nonce.
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data: blob:; " +
                    "font-src 'self' data:; " +
                    "connect-src 'self' ws: wss: http: https:; " +
                    "frame-ancestors 'none';";
            }

            // HSTS only when HTTPS — avoid breaking dev over HTTP
            if (context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
