using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _dbContext;

    public RoleRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToUpperInvariant();

        var entity = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalized, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Role?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return null;

        var entity = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == guid, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        var entity = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = role.Name,
            NormalizedName = role.Name.Trim().ToUpperInvariant(),
            Description = role.Description,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _dbContext.Roles.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncPermissionsAsync(entity, role.Permissions, cancellationToken);
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(role.Id, out var guid))
            throw new InvalidOperationException("Invalid role id");

        var entity = await _dbContext.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Role not found");

        entity.Name = role.Name;
        entity.NormalizedName = role.Name.Trim().ToUpperInvariant();
        entity.Description = role.Description;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SyncPermissionsAsync(entity, role.Permissions, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return;

        var entity = await _dbContext.Roles.FindAsync(new object[] { guid }, cancellationToken);
        if (entity is null) return;

        _dbContext.Roles.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Role> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var query = _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Name.ToLower().Contains(term) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        query = (sortBy?.ToLower()) switch
        {
            "name" => sortDesc
                ? query.OrderByDescending(r => r.Name)
                : query.OrderBy(r => r.Name),
            _ => sortDesc
                ? query.OrderByDescending(r => r.CreatedAtUtc)
                : query.OrderBy(r => r.CreatedAtUtc)
        };

        var total = await query.LongCountAsync(cancellationToken);

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MapToDomain).ToList();

        return (items, total);
    }

    private static Role MapToDomain(RoleEntity entity)
    {
        return new Role
        {
            Id = entity.Id.ToString("N"),
            Name = entity.Name,
            Description = entity.Description,
            Permissions = entity.RolePermissions
                .Select(rp => rp.Permission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private async Task SyncPermissionsAsync(RoleEntity roleEntity, List<string> permissionCodes, CancellationToken cancellationToken)
    {
        // remove all existing
        _dbContext.RolePermissions.RemoveRange(roleEntity.RolePermissions);

        if (permissionCodes == null || permissionCodes.Count == 0)
        {
            roleEntity.RolePermissions.Clear();
            return;
        }

        var normalized = permissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            roleEntity.RolePermissions.Clear();
            return;
        }

        var permissions = await _dbContext.Permissions
            .Where(p => normalized.Contains(p.Code))
            .ToListAsync(cancellationToken);

        roleEntity.RolePermissions = permissions
            .Select(p => new RolePermissionEntity
            {
                RoleId = roleEntity.Id,
                PermissionId = p.Id,
                Role = roleEntity,
                Permission = p
            })
            .ToList();
    }
}
