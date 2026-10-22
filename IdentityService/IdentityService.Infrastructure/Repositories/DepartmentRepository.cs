using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly IdentityDbContext _dbContext;

    public DepartmentRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Department?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return null;

        var entity = await _dbContext.Departments
            .FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Departments
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<(IReadOnlyList<Department> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        string? sortDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var query = _dbContext.Departments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.Code.ToLower().Contains(term) ||
                d.Name.ToLower().Contains(term));
        }

        bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "code" => desc
                ? query.OrderByDescending(d => d.Code)
                : query.OrderBy(d => d.Code),
            "name" => desc
                ? query.OrderByDescending(d => d.Name)
                : query.OrderBy(d => d.Name),
            _ => desc
                ? query.OrderByDescending(d => d.CreatedAtUtc)
                : query.OrderBy(d => d.CreatedAtUtc)
        };

        var total = await query.LongCountAsync(cancellationToken);

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entities.Select(MapToDomain).ToList(), total);
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Departments
            .OrderBy(d => d.Code)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task CreateAsync(Department department, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(department);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            department.Id = entity.Id.ToString("N");
        }

        entity.CreatedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = entity.CreatedAtUtc;

        await _dbContext.Departments.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(department.Id, out var guid))
            throw new InvalidOperationException("Invalid department id");

        var entity = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Department not found");

        entity.Name = department.Name;
        entity.Description = department.Description;
        entity.ParentId = string.IsNullOrWhiteSpace(department.ParentId)
            ? null
            : Guid.Parse(department.ParentId);
        entity.IsActive = department.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return;

        var entity = await _dbContext.Departments.FindAsync(new object[] { guid }, cancellationToken);
        if (entity is null) return;

        _dbContext.Departments.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Department MapToDomain(DepartmentEntity e) => new()
    {
        Id = e.Id.ToString("N"),
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        ParentId = e.ParentId.HasValue ? e.ParentId.Value.ToString("N") : null,
        IsActive = e.IsActive,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc
    };

    private static DepartmentEntity MapToEntity(Department d)
    {
        var entity = new DepartmentEntity();

        if (Guid.TryParse(d.Id, out var guid))
            entity.Id = guid;

        entity.Code = d.Code;
        entity.Name = d.Name;
        entity.Description = d.Description;
        entity.ParentId = string.IsNullOrWhiteSpace(d.ParentId)
            ? null
            : Guid.Parse(d.ParentId);
        entity.IsActive = d.IsActive;
        entity.CreatedAtUtc = d.CreatedAtUtc;
        entity.UpdatedAtUtc = d.UpdatedAtUtc;

        return entity;
    }
}
