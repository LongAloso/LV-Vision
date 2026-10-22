using FluentValidation;
using IdentityService.Application.Configuration;
using IdentityService.Application.Validation;
using IdentityService.Application.Services;
using IdentityService.Infrastructure;
using IdentityService.Api.Common;
using Microsoft.OpenApi.Models;
using IdentityService.Domain.Security;
using IdentityService.Api.Extensions;
using IdentityService.Api.Config;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

LocalSharedConfigBootstrap.Apply(builder);
// Security policy settings
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));

// Infrastructure & application services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<IdentityDbContext>("database", tags: ["ready"])
    .AddCheck<IdentityService.Api.Health.AuditOutboxHealthCheck>(
        "audit-outbox",
        tags: ["ready"]);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<CustomFieldService>();
builder.Services.AddScoped<UserSettingService>();

// FluentValidation - Manual validation trong services
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// Exception handling middleware
builder.Services.AddTransient<ExceptionHandlingMiddleware>();
builder.Services.AddTransient<PasswordChangeEnforcementMiddleware>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger + JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "IdentityService API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập 'Bearer {token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new[] { "Bearer" } }
    });
});

builder.Services.AddIdentityServiceAuth(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = Math.Clamp(
        builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1),
        1,
        5);

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
});

// P2.3: Rate limit auth endpoints per-IP to mitigate brute force across accounts.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"code\":\"rate_limited\",\"message\":\"Too many requests. Please try again later.\"}",
            ct);
    };

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Policy-based authorization using permission claim
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InternalService", policy =>
    {
        policy.AddAuthenticationSchemes(IdentityService.Api.Security.InternalApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("internal_service", "true");
    });
    // ===== Users =====
    options.AddPolicy(SpcPermissions.Users.Manage, policy => policy.RequireClaim("permission", SpcPermissions.Users.Manage));
    options.AddPolicy(SpcPermissions.Users.Read, policy => policy.RequireClaim("permission", SpcPermissions.Users.Read, SpcPermissions.Users.Manage));
    // ===== Roles =====
    options.AddPolicy(SpcPermissions.Roles.Manage, policy => policy.RequireClaim("permission", SpcPermissions.Roles.Manage));
    options.AddPolicy(SpcPermissions.Roles.Read, policy => policy.RequireClaim("permission", SpcPermissions.Roles.Read, SpcPermissions.Roles.Manage));
    // ===== Permissions =====
    options.AddPolicy(SpcPermissions.Permissions.Manage, policy => policy.RequireClaim(
        "permission",
        SpcPermissions.Permissions.Manage,
        SpcPermissions.Users.Manage)); // temporary legacy alias
    options.AddPolicy(SpcPermissions.Permissions.Read, policy => policy.RequireClaim(
        "permission",
        SpcPermissions.Permissions.Read,
        SpcPermissions.Permissions.Manage,
        SpcPermissions.Users.Read,
        SpcPermissions.Users.Manage)); // temporary legacy aliases
    // ===== Departments =====
    options.AddPolicy(SpcPermissions.Departments.Manage, policy => policy.RequireClaim("permission", SpcPermissions.Departments.Manage));
    options.AddPolicy(SpcPermissions.Departments.Read, policy => policy.RequireClaim("permission", SpcPermissions.Departments.Read, SpcPermissions.Departments.Manage));
    // ===== CustomFields =====
    options.AddPolicy(SpcPermissions.CustomFields.Manage, policy => policy.RequireClaim("permission", SpcPermissions.CustomFields.Manage));
    options.AddPolicy(SpcPermissions.CustomFields.Read, policy => policy.RequireClaim("permission", SpcPermissions.CustomFields.Read, SpcPermissions.CustomFields.Manage));
});

var app = builder.Build();

// Must run before routing/rate limiting so RemoteIpAddress is the trusted
// original client rather than the ApiGateway container address.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    await EnsureDevelopmentSchemaAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IdentityService API v1");
        // GIỮ TOKEN SAU KHI REFRESH
        c.ConfigObject.AdditionalItems["persistAuthorization"] = true;
    });
}

app.UseRouting();

// P2.3: Rate limiter must run after routing so it can attach policy by endpoint.
app.UseRateLimiter();

// Global exception middleware (bắt validation và format response)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
// Enforce password change requirement (first login / expired password)
app.UseMiddleware<PasswordChangeEnforcementMiddleware>();

app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => !check.Tags.Contains("live") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => !check.Tags.Contains("live") });
app.MapControllers();

app.Run();

static async Task EnsureDevelopmentSchemaAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    if (!db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
        return;
    }

    // A fresh local SQL Server volume has no application database yet.
    // Apply the existing migrations once; keep the legacy schema patch below
    // for developer databases that were created before migration tracking.
    if (!await db.Database.CanConnectAsync())
    {
        await db.Database.MigrateAsync();
    }

    // Existing dev databases may predate EF migration history; patch only the columns
    // required by the current auth flow instead of replaying the initial migration.
    await db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('dbo.users', 'authz_version') IS NULL
            ALTER TABLE [dbo].[users] ADD [authz_version] bigint NOT NULL
                CONSTRAINT [DF_users_authz_version] DEFAULT (1);

        IF COL_LENGTH('dbo.users', 'active_session_id') IS NULL
            ALTER TABLE [dbo].[users] ADD [active_session_id] uniqueidentifier NULL;

        IF COL_LENGTH('dbo.users', 'measurement_display_mode') IS NULL
            ALTER TABLE [dbo].[users] ADD [measurement_display_mode] tinyint NULL;

        IF COL_LENGTH('dbo.refresh_tokens', 'session_id') IS NULL
            ALTER TABLE [dbo].[refresh_tokens] ADD [session_id] uniqueidentifier NULL;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE [name] = N'IX_refresh_tokens_user_id_session_id'
              AND [object_id] = OBJECT_ID(N'[dbo].[refresh_tokens]')
        )
            CREATE INDEX [IX_refresh_tokens_user_id_session_id]
            ON [dbo].[refresh_tokens] ([user_id], [session_id]);
        """);
}
