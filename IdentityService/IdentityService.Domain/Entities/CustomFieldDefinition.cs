using System.Text.Json;

namespace IdentityService.Domain.Entities;

/// <summary>
/// Domain model for custom field definition.
/// </summary>
public sealed class CustomFieldDefinition
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
