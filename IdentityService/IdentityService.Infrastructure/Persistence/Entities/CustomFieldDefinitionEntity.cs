using System.Text.Json;
using IdentityService.Domain.Entities;

namespace IdentityService.Infrastructure.Persistence.Entities;

public class CustomFieldDefinitionEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Target entity type. Example: measurement_requests, parts, gages.
    /// </summary>
    public string EntityType { get; set; } = default!;

    /// <summary>
    /// Key used in attributes JSON. Example: projectCode.
    /// </summary>
    public string FieldKey { get; set; } = default!;

    public string Label { get; set; } = default!;

    public CustomFieldDataType DataType { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;

    public List<string>? EnumOptions { get; set; }

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? RegexPattern { get; set; }

    public JsonElement? DefaultValue { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
