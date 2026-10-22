using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly IdentityDbContext _dbContext;

    public PermissionRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Permission?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var code = name.Trim();

        var entity = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Permission?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return null;

        var entity = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == guid, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Permissions
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task CreateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        var entity = new PermissionEntity
        {
            Id = Guid.NewGuid(),
            Code = permission.Name.Trim(),
            Name = permission.Name.Trim(),
            Description = permission.Description,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _dbContext.Permissions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        permission.Id = entity.Id.ToString("N");
    }
    public async Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(permission.Id, out var guid))
            throw new InvalidOperationException("Invalid permision id");

        var entity = await _dbContext.Permissions
            .FirstOrDefaultAsync(r => r.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Permision not found");

        entity.Name = permission.Name;
        entity.Code = permission.Name;
        entity.Description = permission.Description;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return;

        var entity = await _dbContext.Permissions.FindAsync(new object[] { guid }, cancellationToken);
        if (entity is null) return;

        _dbContext.Permissions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Permission> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var query = _dbContext.Permissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "name" => sortDesc
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            "description" => sortDesc
                ? query.OrderByDescending(p => p.Description)
                : query.OrderBy(p => p.Description),
            _ => sortDesc
                ? query.OrderByDescending(p => p.CreatedAtUtc)
                : query.OrderBy(p => p.CreatedAtUtc)
        };

        var total = await query.LongCountAsync(cancellationToken);

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MapToDomain).ToList();

        return (items, total);
    }

    private static Permission MapToDomain(PermissionEntity entity)
    {
        return new Permission
        {
            Id = entity.Id.ToString("N"),
            Name = entity.Name,
            Description = entity.Description
        };
    }
}
