using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface ICustomFieldDefinitionRepository
{
    Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(string? entityType, CancellationToken ct = default);
    Task<IReadOnlyList<CustomFieldDefinition>> GetActiveAsync(string entityType, CancellationToken ct = default);
    Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomFieldDefinition?> GetByKeyAsync(string entityType, string fieldKey, CancellationToken ct = default);

    Task<CustomFieldDefinition> CreateAsync(CustomFieldDefinition definition, CancellationToken ct = default);
    Task UpdateAsync(CustomFieldDefinition definition, CancellationToken ct = default);
}
