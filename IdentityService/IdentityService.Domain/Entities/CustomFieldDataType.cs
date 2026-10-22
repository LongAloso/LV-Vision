namespace IdentityService.Domain.Entities;

/// <summary>
/// Supported data types for Custom Fields.
/// Stored in DB as int.
/// </summary>
public enum CustomFieldDataType
{
    String = 0,
    Number = 1,
    Int = 2,
    Bool = 3,
    Date = 4,
    DateTime = 5,
    Enum = 6
}
