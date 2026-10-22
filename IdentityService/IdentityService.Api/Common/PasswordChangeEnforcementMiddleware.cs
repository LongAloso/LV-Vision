using System.Security.Claims;

namespace IdentityService.Api.Common;

/// <summary>
/// Blocks authenticated requests when the user is required to change password.
/// Allowed paths: auth/login, auth/refresh, auth/logout, auth/change-password, swagger.
/// </summary>
public sealed class PasswordChangeEnforcementMiddleware : IMiddleware
{
    private static readonly string[] AllowedPrefixes =
    [
        "/swagger",
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/auth/change-password"
    ];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var required = user.FindFirstValue("pwd_change_required");
            if (string.Equals(required, "true", StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var allowed = AllowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ApiError
                    {
                        Code = "PASSWORD_CHANGE_REQUIRED",
                        Message = "Password change is required before using the system."
                    });
                    return;
                }
            }
        }

        await next(context);
    }
}
