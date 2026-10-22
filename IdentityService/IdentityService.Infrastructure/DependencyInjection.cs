using IdentityService.Application.Configuration;
using IdentityService.Application.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Audit;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Repositories;
using IdentityService.Infrastructure.Security;
using IdentityService.Infrastructure.Seeding;
using IdentityService.Infrastructure.Validation;
using IdentityService.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SpcDatabase")
            ?? throw new InvalidOperationException("Connection string 'SpcDatabase' is not configured.");

        // Database provider switching (Postgres / SQL Server / others in the future).
        // Domain + Application layers stay provider-agnostic; only Infrastructure config changes.
        var provider = configuration["Database:Provider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
            provider = "Postgres";

        services.AddDbContext<IdentityDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "postgres":
                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;

                case "sqlserver":
                case "mssql":
                    options.UseSqlServer(connectionString);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported database provider '{provider}'. Supported: Postgres, SqlServer.");
            }
        });

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IUserDepartmentRepository, UserDepartmentRepository>();
        services.AddScoped<ICustomFieldDefinitionRepository, CustomFieldDefinitionRepository>();
        services.AddScoped<IUserSettingRepository, UserSettingRepository>();
        services.AddScoped<IUserSettingReferenceValidator, UserSettingReferenceValidator>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // P3.4: Audit event publisher (RabbitMQ). Bound from "RabbitMq" + "Audit" config.
        services.Configure<RabbitMqAuditSettings>(opts =>
        {
            opts.Host = configuration["RabbitMq:Host"] ?? opts.Host;
            opts.Port = int.TryParse(configuration["RabbitMq:Port"], out var port) ? port : opts.Port;
            opts.UserName = configuration["RabbitMq:Username"] ?? opts.UserName;
            opts.Password = configuration["RabbitMq:Password"] ?? opts.Password;
            opts.Exchange = configuration["Audit:Exchange"] ?? opts.Exchange;
            opts.OutboxPath = configuration["Audit:OutboxPath"] ?? opts.OutboxPath;
            opts.FlushIntervalSeconds = configuration.GetValue("Audit:FlushIntervalSeconds", opts.FlushIntervalSeconds);
            opts.FlushBatchSize = configuration.GetValue("Audit:FlushBatchSize", opts.FlushBatchSize);
            opts.MaxPendingFiles = configuration.GetValue("Audit:MaxPendingFiles", opts.MaxPendingFiles);
            opts.PublishConfirmTimeoutSeconds = configuration.GetValue(
                "Audit:PublishConfirmTimeoutSeconds",
                opts.PublishConfirmTimeoutSeconds);
            opts.Environment = configuration["ASPNETCORE_ENVIRONMENT"]
                ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        });
        services.AddSingleton<AuditOutboxStore>();
        services.AddSingleton(sp => new RabbitMqAuditEventPublisher(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMqAuditSettings>>(),
            sp.GetRequiredService<AuditOutboxStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMqAuditEventPublisher>>()));
        services.AddSingleton<IAuditEventPublisher>(sp => sp.GetRequiredService<RabbitMqAuditEventPublisher>());
        services.AddHostedService(sp => sp.GetRequiredService<RabbitMqAuditEventPublisher>());

        services.AddHostedService<DataSeederHostedService>();

        return services;
    }
}
