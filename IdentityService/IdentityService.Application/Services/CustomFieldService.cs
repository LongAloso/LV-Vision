using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityService.Application.DTOs.CustomFields;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Services;

/// <summary>
/// Application service for managing Custom Field definitions.
/// Follows the same pattern as UserService/DepartmentService:
/// - depends only on Domain abstractions
/// - repositories handle persistence (SaveChanges)
/// </summary>
public sealed class CustomFieldService
{
    private static readonly Regex KeyRegex = new("^[a-z][a-zA-Z0-9_]{1,49}$", RegexOptions.Compiled);

    private readonly ICustomFieldDefinitionRepository _repo;

    public CustomFieldService(ICustomFieldDefinitionRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> GetAllAsync(string? entityType, CancellationToken ct = default)
    {
        var defs = await _repo.GetAllAsync(NormalizeEntityType(entityType), ct);
        return defs.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ActiveCustomFieldDefinitionDto>> GetActiveAsync(string entityType, CancellationToken ct = default)
    {
        var type = NormalizeEntityType(entityType) ?? throw new InvalidOperationException("entityType is required");
        var defs = await _repo.GetActiveAsync(type, ct);
        return defs.Select(MapToActiveDto).ToList();
    }

    public async Task<CustomFieldDefinitionDto> CreateAsync(CreateCustomFieldRequest request, CancellationToken ct = default)
    {
        ValidateCreate(request);

        var type = NormalizeEntityType(request.EntityType) ?? throw new InvalidOperationException("entityType is required");
        var key = request.FieldKey.Trim();

        var existing = await _repo.GetByKeyAsync(type, key, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Custom field '{type}:{key}' already exists.");

        var now = DateTimeOffset.UtcNow;
        var def = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            EntityType = type,
            FieldKey = key,
            Label = request.Label.Trim(),
            DataType = request.DataType,
            IsRequired = request.IsRequired,
            IsActive = true,
            EnumOptions = request.EnumOptions?.ToList(),
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            RegexPattern = request.RegexPattern,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            DefaultValueRaw = request.DefaultValue is null
                ? (JsonElement?)null
                : JsonSerializer.SerializeToElement(request.DefaultValue)
        };

        var created = await _repo.CreateAsync(def, ct);
        return MapToDto(created);
    }

    public async Task<CustomFieldDefinitionDto?> UpdateAsync(Guid id, UpdateCustomFieldRequest request, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null) return null;

        ValidateUpdate(existing, request);

        existing.Label = request.Label.Trim();
        existing.IsRequired = request.IsRequired;
        existing.IsActive = request.IsActive;
        existing.EnumOptions = request.EnumOptions?.ToList();
        existing.MinValue = request.MinValue;
        existing.MaxValue = request.MaxValue;
        existing.RegexPattern = request.RegexPattern;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        existing.DefaultValueRaw = request.DefaultValue is null
            ? (JsonElement?)null
            : JsonSerializer.SerializeToElement(request.DefaultValue);

        await _repo.UpdateAsync(existing, ct);

        var reloaded = await _repo.GetByIdAsync(id, ct);
        return reloaded is null ? null : MapToDto(reloaded);
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null) return false;

        existing.IsActive = false;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(existing, ct);
        return true;
    }

    private static void ValidateCreate(CreateCustomFieldRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.EntityType))
            throw new InvalidOperationException("entityType is required.");
        if (!KeyRegex.IsMatch(req.FieldKey ?? string.Empty))
            throw new InvalidOperationException("fieldKey invalid. Use ^[a-z][a-zA-Z0-9_]{1,49}$");
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new InvalidOperationException("label is required.");
        if (req.DataType == CustomFieldDataType.Enum && (req.EnumOptions is null || req.EnumOptions.Count == 0))
            throw new InvalidOperationException("enumOptions is required for Enum.");
        if (req.MinValue.HasValue && req.MaxValue.HasValue && req.MinValue > req.MaxValue)
            throw new InvalidOperationException("minValue must be <= maxValue.");
    }

    private static void ValidateUpdate(CustomFieldDefinition existing, UpdateCustomFieldRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new InvalidOperationException("label is required.");
        if (existing.DataType == CustomFieldDataType.Enum && (req.EnumOptions is null || req.EnumOptions.Count == 0))
            throw new InvalidOperationException("enumOptions is required for Enum.");
        if (req.MinValue.HasValue && req.MaxValue.HasValue && req.MinValue > req.MaxValue)
            throw new InvalidOperationException("minValue must be <= maxValue.");
    }

    private static string? NormalizeEntityType(string? entityType)
        => string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim().ToLowerInvariant();

    private static CustomFieldDefinitionDto MapToDto(CustomFieldDefinition x)
    {
        object? defVal = null;
        if (x.DefaultValueRaw.HasValue)
        {
            try { defVal = JsonSerializer.Deserialize<object>(x.DefaultValueRaw.Value.GetRawText()); }
            catch { defVal = x.DefaultValueRaw.Value.GetRawText(); }
        }

        return new CustomFieldDefinitionDto(
            x.Id,
            x.EntityType,
            x.FieldKey,
            x.Label,
            x.DataType,
            x.IsRequired,
            x.IsActive,
            x.EnumOptions,
            x.MinValue,
            x.MaxValue,
            x.RegexPattern,
            defVal,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }

    private static ActiveCustomFieldDefinitionDto MapToActiveDto(CustomFieldDefinition x)
        => new()
        {
            Id = x.Id,
            EntityType = x.EntityType,
            FieldKey = x.FieldKey,
            Label = x.Label,
            DataType = x.DataType,
            IsRequired = x.IsRequired,
            IsActive = x.IsActive,
            EnumOptions = x.EnumOptions,
            MinValue = x.MinValue,
            MaxValue = x.MaxValue,
            RegexPattern = x.RegexPattern,
            DefaultValueRaw = x.DefaultValueRaw,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        };
}
