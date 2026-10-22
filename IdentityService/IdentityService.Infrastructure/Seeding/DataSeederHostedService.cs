using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Seeding;

public class DataSeederHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSeederHostedService> _logger;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public DataSeederHostedService(
        IServiceProvider serviceProvider,
        ILogger<DataSeederHostedService> logger,
        IHostEnvironment env,
        IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _env = env;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roleRepo = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var permRepo = scope.ServiceProvider.GetRequiredService<IPermissionRepository>();
        var deptRepo = scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();
        var userDeptRepo = scope.ServiceProvider.GetRequiredService<IUserDepartmentRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        async Task EnsurePermissionsAsync(IEnumerable<string> permissionNames)
        {
            foreach (var p in permissionNames
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var existing = await permRepo.GetByNameAsync(p, cancellationToken);
                if (existing is not null) continue;

                existing = new Permission
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = p,
                    Description = $"Permission {p}"
                };
                await permRepo.CreateAsync(existing, cancellationToken);
            }
        }

        async Task EnsureRoleContainsPermissionsAsync(string roleName, IEnumerable<string> permissionNames)
        {
            var role = await roleRepo.GetByNameAsync(roleName, cancellationToken);
            if (role is null) return;

            var currentPermissions = role.Permissions ?? new List<string>();
            var permissions = new HashSet<string>(
                currentPermissions.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var changed = currentPermissions.Count != permissions.Count
                || currentPermissions.Any(x => string.IsNullOrWhiteSpace(x)
                    || !string.Equals(x, x.Trim(), StringComparison.Ordinal)
                    || !permissions.Contains(x.Trim()));

            foreach (var permission in permissionNames
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim()))
            {
                changed |= permissions.Add(permission);
            }

            if (!changed) return;

            role.Permissions = permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            await roleRepo.UpdateAsync(role, cancellationToken);
        }

        string ResolveSeedPassword(string configKey, string devDefault)
        {
            var pwd = _config[configKey];
            if (!string.IsNullOrWhiteSpace(pwd)) return pwd;
            if (_env.IsDevelopment()) return devDefault;
            throw new InvalidOperationException(
                $"Seed password missing. Set config key {configKey} (env: {configKey.Replace(":", "__")}) or run in Development.");
        }

        var dataCollectionPerms = new[]
        {
            "DataCollection.Ingestion.Write",
            "DataCollection.Ingestion.Debug.Read",
            "DataCollection.Manage"
        };

        var measurementPerms = new[]
        {
            "Measurement.Requests.Read",
            "Measurement.Requests.Create",
            "Measurement.Requests.Update",
            "Measurement.Requests.Delete",
            "Measurement.Requests.ExportExcel",
            "Measurement.Requests.Manage",
            "Measurement.Comments.Read",
            "Measurement.Comments.Create",
            "Measurement.Comments.Update",
            "Measurement.Comments.Delete",
            "Excel.Template.Manage",
            "Excel.Template.Read",
            "Excel.Template.Download",
            "Excel.Template.Delete",
            "Excel.Template.GenTag",
            "ShiftConfigs.Read",
            "ShiftConfigs.Manage"
        };

        var documentPerms = new[]
        {
            "Documents.Create",
            "Documents.Read.Own",
            "Documents.Read.All",
            "Documents.Update.Own",
            "Documents.Update.All",
            "Documents.Delete.Own",
            "Documents.Delete.All",
            "Documents.File.Upload",
            "Documents.File.Download.Own",
            "Documents.File.Download.All",
            "DocumentGroups.Read",
            "DocumentGroups.Manage"
        };

        var workflowPerms = new[]
        {
            "WorkflowDefinitions.Read",
            "WorkflowDefinitions.Manage"
        };

        var gageDailyCheckPerms = new[]
        {
            "Gages.DailyChecks.Read",
            "Gages.DailyChecks.Manage"
        };

        var workCalendarPerms = new[]
        {
            "WorkCalendars.Read",
            "WorkCalendars.Manage"
        };

        var servicePerms = dataCollectionPerms
            .Concat(measurementPerms)
            .Concat(documentPerms)
            .Concat(workflowPerms)
            .Concat(gageDailyCheckPerms)
            .Concat(workCalendarPerms)
            .ToArray();

        var allPerms = SpcPermissions.Canonical
            .Concat(SpcPermissions.Legacy)
            .Concat(servicePerms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var moderatorPerms = SpcPermissions.ModeratorRead
            // Retain read aliases during the permission-name transition.
            .Concat(SpcPermissions.Legacy.Where(x => x.EndsWith(".Read", StringComparison.OrdinalIgnoreCase)))
            .Append("Measurement.Requests.Read")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await userRepo.AnyAsync(cancellationToken))
        {
            await EnsurePermissionsAsync(allPerms);
            await EnsureRoleContainsPermissionsAsync("Admin", allPerms);
            await EnsureRoleContainsPermissionsAsync("Mod", moderatorPerms);
        }

        // Chỉ seed khi database chưa có user nào
        if (!await userRepo.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seeding permissions, departments, roles and admin/mod users...");

            // 1) Permissions
            var perms = allPerms;

            await EnsurePermissionsAsync(perms);

            // 2) Departments: IT, HR, FIN
            async Task<Department> EnsureDepartmentAsync(string code, string name, string? description)
            {
                var dept = await deptRepo.GetByCodeAsync(code, cancellationToken);
                if (dept is null)
                {
                    dept = new Department
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = code,
                        Name = name,
                        Description = description,
                        IsActive = true,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                    await deptRepo.CreateAsync(dept, cancellationToken);
                }
                return dept;
            }

            var itDept = await EnsureDepartmentAsync("IT", "Phòng CNTT", "Phòng Công nghệ thông tin");
            var hrDept = await EnsureDepartmentAsync("HR", "Phòng Nhân sự", "Phòng Nhân sự");
            var finDept = await EnsureDepartmentAsync("FIN", "Phòng Tài chính", "Phòng Tài chính – Kế toán");

            // 3) Roles: Admin, Mod
            var allPermCodes = perms.ToList();

            // Admin: có tất cả quyền
            var adminRole = await roleRepo.GetByNameAsync("Admin", cancellationToken);
            if (adminRole is null)
            {
                adminRole = new Role
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Admin",
                    Description = "System administrator",
                    Permissions = allPermCodes
                };
                await roleRepo.CreateAsync(adminRole, cancellationToken);
            }
            else
            {
                adminRole.Permissions = allPermCodes;
                await roleRepo.UpdateAsync(adminRole, cancellationToken);
            }

            // Mod: gán quyền đọc cơ bản
            var modPerms = moderatorPerms.ToList();

            var modRole = await roleRepo.GetByNameAsync("Mod", cancellationToken);
            if (modRole is null)
            {
                modRole = new Role
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Mod",
                    Description = "Moderator",
                    Permissions = modPerms
                };
                await roleRepo.CreateAsync(modRole, cancellationToken);
            }
            else
            {
                modRole.Permissions = modPerms;
                await roleRepo.UpdateAsync(modRole, cancellationToken);
            }

            // 4) Users: admin & mod
            var adminPwd = ResolveSeedPassword("Seed:AdminPassword", "Admin@123");
            var modPwd = ResolveSeedPassword("Seed:ModPassword", "Mod@123");
            var mustChangePassword = !_env.IsDevelopment();

            var adminUser = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = "admin",
                Email = "admin@example.com",
                PasswordHash = hasher.HashPassword(adminPwd),
                Roles = new List<string> { "Admin" },
                IsActive = true,
                MustChangePassword = mustChangePassword,
                PasswordChangedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var modUser = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = "mod",
                Email = "mod@example.com",
                PasswordHash = hasher.HashPassword(modPwd),
                Roles = new List<string> { "Mod" },
                IsActive = true,
                MustChangePassword = mustChangePassword,
                PasswordChangedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await userRepo.CreateAsync(adminUser, cancellationToken);
            await userRepo.CreateAsync(modUser, cancellationToken);

            // 5) Gán user vào phòng ban IT (phòng ban chính)
            var adminDepartments = new List<(string DepartmentId, bool IsPrimary)>
            {
                (itDept.Id, true)
            };

            var modDepartments = new List<(string DepartmentId, bool IsPrimary)>
            {
                (itDept.Id, true)
            };

            await userDeptRepo.AssignDepartmentsAsync(adminUser.Id, adminDepartments, cancellationToken);
            await userDeptRepo.AssignDepartmentsAsync(modUser.Id, modDepartments, cancellationToken);

            _logger.LogInformation(
                "Seeded default admin/mod users in department IT. MustChangePassword={MustChangePassword}",
                mustChangePassword);
        }
        else
        {
            _logger.LogInformation("Users already exist, skipping seeding.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
