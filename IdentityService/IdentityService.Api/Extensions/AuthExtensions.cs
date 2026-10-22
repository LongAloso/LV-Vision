using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Api.Security;
using IdentityService.Application.Configuration;
using IdentityService.Application.Security;
using IdentityService.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddIdentityServiceAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10_000;
        });

        services.AddScoped<IIdentityPermissionClient, LocalIdentityPermissionClient>();
        services.AddScoped<IPermissionSnapshotService, MemoryPermissionSnapshotService>();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        var securitySettings = configuration.GetSection("Security").Get<SecuritySettings>() ?? new SecuritySettings();
        var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "Development",
                    StringComparison.OrdinalIgnoreCase);
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var identity = principal?.Identity as ClaimsIdentity;

                        if (principal is null || identity is null)
                        {
                            context.Fail("Invalid principal.");
                            return;
                        }

                        var userId = principal.FindFirst("uid")?.Value
                                     ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                     ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            context.Fail("Missing uid claim.");
                            return;
                        }

                        var requestServices = context.HttpContext.RequestServices;

                        if (securitySettings.SingleClientLoginEnabled)
                        {
                            var tokenSessionId = principal.FindFirst("session_id")?.Value;
                            var userRepository = requestServices.GetRequiredService<IUserRepository>();
                            var activeSessionId = await userRepository.GetActiveSessionIdAsync(userId, context.HttpContext.RequestAborted);

                            if (string.IsNullOrWhiteSpace(tokenSessionId) ||
                                !activeSessionId.HasValue ||
                                !string.Equals(tokenSessionId, activeSessionId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
                            {
                                context.Fail("Session has been replaced by another client login.");
                                return;
                            }
                        }

                        if (principal.Claims.Any(c => c.Type == "permission"))
                            return;

                        if (!long.TryParse(principal.FindFirst("authz_version")?.Value, out var authzVersion) || authzVersion <= 0)
                        {
                            var identityPermissionClient = requestServices.GetRequiredService<IIdentityPermissionClient>();
                            var me = await identityPermissionClient.GetCurrentUserAsync(userId, context.HttpContext.RequestAborted);
                            if (me is null)
                            {
                                context.Fail("Unable to resolve current user permissions.");
                                return;
                            }

                            foreach (var permission in me.Permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
                            {
                                if (!identity.HasClaim("permission", permission))
                                    identity.AddClaim(new Claim("permission", permission));
                            }

                            if (!identity.HasClaim(c => c.Type == "authz_version"))
                                identity.AddClaim(new Claim("authz_version", me.AuthzVersion.ToString()));

                            return;
                        }

                        var permissionService = requestServices.GetRequiredService<IPermissionSnapshotService>();

                        var snapshot = await permissionService.GetPermissionsAsync(
                            userId,
                            authzVersion,
                            context.HttpContext.RequestAborted);

                        if (snapshot is null)
                        {
                            context.Fail("Authorization snapshot is missing or outdated.");
                            return;
                        }

                        foreach (var permission in snapshot.Permissions)
                        {
                            if (!identity.HasClaim("permission", permission))
                                identity.AddClaim(new Claim("permission", permission));
                        }
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
                InternalApiKeyAuthenticationHandler.SchemeName,
                _ => { });

        return services;
    }
}
