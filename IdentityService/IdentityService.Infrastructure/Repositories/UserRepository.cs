using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<(IReadOnlyList<User> Items, long TotalItems)>>> SearchFlights = new();
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return null;

        var entity = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments)
                .ThenInclude(ud => ud.Department)
            .FirstOrDefaultAsync(u => u.Id == guid, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments)
                .ThenInclude(ud => ud.Department)
            .FirstOrDefaultAsync(u => u.UserName.Trim().ToUpper() == userName.Trim().ToUpper(), cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }
    public async Task<Guid?> GetUserIdByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserName.Trim().ToUpper() == userName.Trim().ToUpper(), cancellationToken);

        return entity is null ? Guid.Empty : entity.Id;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments)
                .ThenInclude(ud => ud.Department)
            .FirstOrDefaultAsync(u => u.Email.ToUpper() == email.Trim().ToUpper(), cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<User?> GetByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments)
                .ThenInclude(ud => ud.Department)
            .FirstOrDefaultAsync(u =>
                u.UserName.Trim().ToUpper() == userNameOrEmail.Trim().ToUpper() ||
                u.Email.Trim().ToUpper() == userNameOrEmail.Trim().ToUpper(),
                cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(user);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            user.Id = entity.Id.ToString("N");
        }

        entity.CreatedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = entity.CreatedAtUtc;

        await _dbContext.Users.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // =========================
        // sync roles (giữ nguyên)
        // =========================
        var requestedRoleNames = new HashSet<string>(
            user.Roles ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (requestedRoleNames.Count > 0)
        {
            var roles = await _dbContext.Roles
                .Where(r => requestedRoleNames.Contains(r.Name))
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                _dbContext.UserRoles.Add(new UserRoleEntity
                {
                    UserId = entity.Id,
                    RoleId = role.Id
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // =========================
        // sync departments (NEW)
        // =========================
        var requestedDepartments = (user.Departments ?? Enumerable.Empty<string>())
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (requestedDepartments.Count > 0)
        {
            // optional: validate departments exist
            var existingDeptIds = await _dbContext.Departments
                .Where(d => requestedDepartments.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            foreach (var deptId in existingDeptIds)
            {
                _dbContext.UserDepartments.Add(new UserDepartmentEntity
                {
                    UserId = entity.Id,
                    DepartmentId = deptId,
                });
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }


    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(user.Id, out var guid))
            throw new InvalidOperationException("Invalid user id");

        var entity = await _dbContext.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments).ThenInclude(ud => ud.Department)
            .FirstOrDefaultAsync(u => u.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("User not found");

        entity.UserName = user.UserName.Trim();
        entity.FullName = string.IsNullOrWhiteSpace(user.FullName) ? entity.UserName : user.FullName.Trim();
        entity.Email = user.Email.Trim();
        entity.NormalizedEmail = entity.Email.ToUpperInvariant();
        entity.PasswordHash = user.PasswordHash;
        entity.IsActive = user.IsActive;
        entity.AccessFailedCount = user.AccessFailedCount;
        entity.LockoutEnabled = user.LockoutEnabled;
        entity.LockoutEndUtc = user.LockoutEndUtc;
        entity.MustChangePassword = user.MustChangePassword;
        entity.PasswordChangedAtUtc = user.PasswordChangedAtUtc;
        entity.LastLoginAtUtc = user.LastLoginAtUtc;
        entity.ActiveSessionId = user.ActiveSessionId;
        entity.AuthzVersion = user.AuthzVersion <= 0 ? entity.AuthzVersion : user.AuthzVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        // update additional fields
        entity.DisplayDefault = user.DisplayDefault;
        entity.MeasurementType = user.MeasurementType;
        entity.MeasurementDisplayMode = user.MeasurementDisplayMode;
        entity.PhoneNumber = user.PhoneNumber;
        entity.ProfileImageUrl = user.ProfileImageUrl;

        // =========================
        // sync roles (giữ nguyên code bạn)
        // =========================
        var requestedRoleNames = new HashSet<string>(user.Roles ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        var toRemove = entity.UserRoles
            .Where(ur => !requestedRoleNames.Contains(ur.Role.Name))
            .ToList();

        foreach (var r in toRemove)
            entity.UserRoles.Remove(r);

        var existingRoleNames = entity.UserRoles
            .Select(ur => ur.Role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rolesToAdd = requestedRoleNames.Except(existingRoleNames).ToList();

        if (rolesToAdd.Count > 0)
        {
            var roles = await _dbContext.Roles
                .Where(r => rolesToAdd.Contains(r.Name))
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                entity.UserRoles.Add(new UserRoleEntity
                {
                    UserId = entity.Id,
                    RoleId = role.Id,
                    User = entity,
                    Role = role
                });
            }
        }

        // =========================
        // sync departments 
        // =========================
        var requestedDepartments = (user.Departments ?? Enumerable.Empty<string>())
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        // remove old departments not requested
        var deptToRemove = entity.UserDepartments
            .Where(ud => !requestedDepartments.Contains(ud.DepartmentId))
            .ToList();

        foreach (var d in deptToRemove)
            entity.UserDepartments.Remove(d);

        // add new departments
        var existingDeptIds = entity.UserDepartments
            .Select(ud => ud.DepartmentId)
            .ToHashSet();

        var deptIdsToAdd = requestedDepartments
            .Where(id => !existingDeptIds.Contains(id))
            .ToList();

        if (deptIdsToAdd.Count > 0)
        {
            // optional: validate departments exist
            var validDeptIds = await _dbContext.Departments
                .Where(d => deptIdsToAdd.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            foreach (var deptId in validDeptIds)
            {
                entity.UserDepartments.Add(new UserDepartmentEntity
                {
                    UserId = entity.Id,
                    DepartmentId = deptId,
                    User = entity,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSecurityStateAsync(
        string userId,
        int accessFailedCount,
        bool lockoutEnabled,
        DateTimeOffset? lockoutEndUtc,
        bool mustChangePassword,
        DateTimeOffset? passwordChangedAtUtc,
        DateTimeOffset? lastLoginAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            throw new InvalidOperationException("Invalid user id");

        var entity = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("User not found");

        entity.AccessFailedCount = accessFailedCount;
        entity.LockoutEnabled = lockoutEnabled;
        entity.LockoutEndUtc = lockoutEndUtc;
        entity.MustChangePassword = mustChangePassword;
        entity.PasswordChangedAtUtc = passwordChangedAtUtc;
        entity.LastLoginAtUtc = lastLoginAtUtc;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePasswordAsync(
        string userId,
        string newPasswordHash,
        DateTimeOffset passwordChangedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            throw new InvalidOperationException("Invalid user id");

        var entity = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("User not found");

        entity.PasswordHash = newPasswordHash;
        entity.PasswordChangedAtUtc = passwordChangedAtUtc;
        entity.MustChangePassword = false;
        entity.AccessFailedCount = 0;
        entity.LockoutEnabled = false;
        entity.LockoutEndUtc = null;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return;

        var entity = await _dbContext.Users.FindAsync(new object[] { guid }, cancellationToken);
        if (entity is null) return;

        _dbContext.Users.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => _dbContext.Users.AnyAsync(cancellationToken);

    public async Task<(IReadOnlyList<User> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var key = JsonSerializer.Serialize(new
        {
            Search = search?.Trim(),
            SortBy = sortBy?.Trim(),
            SortDesc = sortDesc,
            Page = page,
            PageSize = pageSize
        });
        var flight = SearchFlights.GetOrAdd(
            key,
            _ => new Lazy<Task<(IReadOnlyList<User> Items, long TotalItems)>>(
                () => SearchCoreAsync(search, sortBy, sortDesc, page, pageSize),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await flight.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (flight.IsValueCreated && flight.Value.IsCompleted)
            {
                ((ICollection<KeyValuePair<string, Lazy<Task<(IReadOnlyList<User> Items, long TotalItems)>>>>)SearchFlights)
                    .Remove(new KeyValuePair<string, Lazy<Task<(IReadOnlyList<User> Items, long TotalItems)>>>(key, flight));
            }
        }
    }

    private async Task<(IReadOnlyList<User> Items, long TotalItems)> SearchCoreAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize)
    {
        var query = _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserDepartments)
                .ThenInclude(ud => ud.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term));
        }

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "username" => sortDesc
                ? query.OrderByDescending(u => u.UserName)
                : query.OrderBy(u => u.UserName),
            "fullname" => sortDesc
                ? query.OrderByDescending(u => u.FullName)
                : query.OrderBy(u => u.FullName),
            "email" => sortDesc
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),
            _ => sortDesc
                ? query.OrderByDescending(u => u.CreatedAtUtc)
                : query.OrderBy(u => u.CreatedAtUtc)
        };

        var total = await query.LongCountAsync();

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entities.Select(MapToDomain).ToList();

        return (items, total);
    }

    private static User MapToDomain(UserEntity entity)
    {
        var roleIds = entity.UserRoles
            .Select(ur => ur.RoleId.ToString("N"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var deptIds = entity.UserDepartments
            .Select(ud => ud.DepartmentId.ToString("N"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var primaryDeptId = entity.UserDepartments
            .Where(ud => ud.IsPrimary)
            .Select(ud => ud.DepartmentId.ToString("N"))
            .FirstOrDefault();

        return new User
        {
            Id = entity.Id.ToString("N"),
            UserName = entity.UserName,
            FullName = entity.FullName,
            Email = entity.Email,
            DisplayDefault = entity.DisplayDefault,
            MeasurementType = entity.MeasurementType,
            MeasurementDisplayMode = entity.MeasurementDisplayMode,
            PhoneNumber = entity.PhoneNumber,
            ProfileImageUrl = entity.ProfileImageUrl,
            PasswordHash = entity.PasswordHash,
            IsActive = entity.IsActive,
            AccessFailedCount = entity.AccessFailedCount,
            LockoutEnabled = entity.LockoutEnabled,
            LockoutEndUtc = entity.LockoutEndUtc,
            MustChangePassword = entity.MustChangePassword,
            PasswordChangedAtUtc = entity.PasswordChangedAtUtc,
            LastLoginAtUtc = entity.LastLoginAtUtc,
            ActiveSessionId = entity.ActiveSessionId,
            Roles = entity.UserRoles.Select(ur => ur.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Departments = deptIds,
            RoleIds = roleIds,
            AuthzVersion = entity.AuthzVersion,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    private UserEntity MapToEntity(User user)
    {
        var entity = new UserEntity();

        if (Guid.TryParse(user.Id, out var guid))
            entity.Id = guid;

        entity.UserName = user.UserName.Trim();
        entity.FullName = string.IsNullOrWhiteSpace(user.FullName) ? entity.UserName : user.FullName.Trim();
        entity.Email = user.Email.Trim();
        entity.DisplayDefault = user.DisplayDefault;
        entity.MeasurementType = user.MeasurementType;
        entity.MeasurementDisplayMode = user.MeasurementDisplayMode;
        entity.PhoneNumber = user.PhoneNumber;
        entity.ProfileImageUrl = user.ProfileImageUrl;
        entity.NormalizedEmail = entity.Email.ToUpperInvariant();
        entity.PasswordHash = user.PasswordHash;
        entity.IsActive = user.IsActive;
        entity.AccessFailedCount = user.AccessFailedCount;
        entity.LockoutEnabled = user.LockoutEnabled;
        entity.LockoutEndUtc = user.LockoutEndUtc;
        entity.MustChangePassword = user.MustChangePassword;
        entity.PasswordChangedAtUtc = user.PasswordChangedAtUtc;
        entity.LastLoginAtUtc = user.LastLoginAtUtc;
        entity.ActiveSessionId = user.ActiveSessionId;
        entity.AuthzVersion = user.AuthzVersion <= 0 ? 1 : user.AuthzVersion;
        entity.CreatedAtUtc = user.CreatedAtUtc;
        entity.UpdatedAtUtc = user.CreatedAtUtc;

        return entity;
    }

    public async Task<Guid?> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            return null;

        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == guid)
            .Select(u => u.ActiveSessionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateActiveSessionAsync(string userId, Guid? activeSessionId, DateTimeOffset? lastLoginAtUtc, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            throw new InvalidOperationException("Invalid user id");

        await _dbContext.Users
            .Where(u => u.Id == guid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.ActiveSessionId, _ => activeSessionId)
                .SetProperty(u => u.LastLoginAtUtc, _ => lastLoginAtUtc)
                .SetProperty(u => u.UpdatedAtUtc, _ => DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task IncrementAuthzVersionAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            throw new InvalidOperationException("Invalid user id");

        await _dbContext.Users
            .Where(u => u.Id == guid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.AuthzVersion, u => u.AuthzVersion + 1)
                .SetProperty(u => u.UpdatedAtUtc, _ => DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task IncrementAuthzVersionForRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(roleId, out var guid))
            return;

        var userIds = await _dbContext.UserRoles
            .Where(ur => ur.RoleId == guid)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return;

        await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.AuthzVersion, u => u.AuthzVersion + 1)
                .SetProperty(u => u.UpdatedAtUtc, _ => DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task IncrementAllAuthzVersionsAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Users
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.AuthzVersion, u => u.AuthzVersion + 1)
                .SetProperty(u => u.UpdatedAtUtc, _ => DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetBasicUsersAsync(CancellationToken ct = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new User
            {
                Id = u.Id.ToString(),
                UserName = u.UserName,
                FullName = u.FullName
            })
            .ToListAsync(ct);
    }
}
