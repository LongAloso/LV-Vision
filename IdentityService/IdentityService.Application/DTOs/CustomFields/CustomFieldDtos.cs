using System.Text.Json;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.DTOs.CustomFields;

public record CustomFieldDefinitionDto(
    Guid Id,
    string EntityType,
    string FieldKey,
    string Label,
    CustomFieldDataType DataType,
    bool IsRequired,
    bool IsActive,
    IReadOnlyList<string>? EnumOptions,
    decimal? MinValue,
    decimal? MaxValue,
    string? RegexPattern,
    object? DefaultValue,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc
);

public record CreateCustomFieldRequest(
    string EntityType,
    string FieldKey,
    string Label,
    CustomFieldDataType DataType,
    bool IsRequired,
    IReadOnlyList<string>? EnumOptions,
    decimal? MinValue,
    decimal? MaxValue,
    string? RegexPattern,
    object? DefaultValue
);

public record UpdateCustomFieldRequest(
    string Label,
    bool IsRequired,
    bool IsActive,
    IReadOnlyList<string>? EnumOptions,
    decimal? MinValue,
    decimal? MaxValue,
    string? RegexPattern,
    object? DefaultValue
);

/// <summary>
/// DTO for MeasurementService consumption (keeps DefaultValue as JsonElement).
/// </summary>
public sealed class ActiveCustomFieldDefinitionDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;
    public string FieldKey { get; set; } = default!;
    public string Label { get; set; } = default!;
    public CustomFieldDataType DataType { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
    public List<string>? EnumOptions { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? RegexPattern { get; set; }
    public JsonElement? DefaultValueRaw { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
