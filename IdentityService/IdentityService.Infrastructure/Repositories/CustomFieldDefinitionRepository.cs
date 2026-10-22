using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public sealed class CustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
{
    private readonly IdentityDbContext _db;

    public CustomFieldDefinitionRepository(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(string? entityType, CancellationToken ct = default)
    {
        var q = _db.CustomFieldDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(x => x.EntityType == entityType);

        var items = await q
            .OrderBy(x => x.EntityType)
            .ThenBy(x => x.FieldKey)
            .ToListAsync(ct);

        return items.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetActiveAsync(string entityType, CancellationToken ct = default)
    {
        var items = await _db.CustomFieldDefinitions.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.IsActive)
            .OrderBy(x => x.FieldKey)
            .ToListAsync(ct);

        return items.Select(MapToDomain).ToList();
    }

    public async Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<CustomFieldDefinition?> GetByKeyAsync(string entityType, string fieldKey, CancellationToken ct = default)
    {
        var entity = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.EntityType == entityType && x.FieldKey == fieldKey, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<CustomFieldDefinition> CreateAsync(CustomFieldDefinition definition, CancellationToken ct = default)
    {
        var entity = MapToEntity(definition);
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            definition.Id = entity.Id;
        }

        if (entity.CreatedAtUtc == default)
            entity.CreatedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = entity.CreatedAtUtc == default ? DateTimeOffset.UtcNow : entity.UpdatedAtUtc;

        await _db.CustomFieldDefinitions.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        var reloaded = await _db.CustomFieldDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.Id, ct);
        return reloaded is null ? definition : MapToDomain(reloaded);
    }

    public async Task UpdateAsync(CustomFieldDefinition definition, CancellationToken ct = default)
    {
        var entity = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == definition.Id, ct);
        if (entity is null)
            throw new InvalidOperationException($"CustomFieldDefinition {definition.Id} not found");

        // Update mutable fields
        entity.Label = definition.Label;
        entity.IsRequired = definition.IsRequired;
        entity.IsActive = definition.IsActive;
        entity.EnumOptions = definition.EnumOptions;
        entity.MinValue = definition.MinValue;
        entity.MaxValue = definition.MaxValue;
        entity.RegexPattern = definition.RegexPattern;
        entity.DefaultValue = definition.DefaultValueRaw;
        entity.UpdatedAtUtc = definition.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : definition.UpdatedAtUtc;

        _db.CustomFieldDefinitions.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static CustomFieldDefinition MapToDomain(CustomFieldDefinitionEntity e) => new()
    {
        Id = e.Id,
        EntityType = e.EntityType,
        FieldKey = e.FieldKey,
        Label = e.Label,
        DataType = e.DataType,
        IsRequired = e.IsRequired,
        IsActive = e.IsActive,
        EnumOptions = e.EnumOptions,
        MinValue = e.MinValue,
        MaxValue = e.MaxValue,
        RegexPattern = e.RegexPattern,
        DefaultValueRaw = e.DefaultValue,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc
    };

    private static CustomFieldDefinitionEntity MapToEntity(CustomFieldDefinition d) => new()
    {
        Id = d.Id,
        EntityType = d.EntityType,
        FieldKey = d.FieldKey,
        Label = d.Label,
        DataType = d.DataType,
        IsRequired = d.IsRequired,
        IsActive = d.IsActive,
        EnumOptions = d.EnumOptions,
        MinValue = d.MinValue,
        MaxValue = d.MaxValue,
        RegexPattern = d.RegexPattern,
        DefaultValue = d.DefaultValueRaw,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc
    };
}
