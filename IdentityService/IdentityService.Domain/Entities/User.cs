namespace IdentityService.Domain.Entities;

public class User
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool? DisplayDefault { get; set; } = true;
    public byte? MeasurementType { get; set; } = default!;
    public byte? MeasurementDisplayMode { get; set; } = 0;
    // Security
    public int AccessFailedCount { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }

    /// <summary>
    /// When true, user must change password before using the system.
    /// Used for first-login enforcement and password-expiry enforcement.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// The last time the password was changed.
    /// Null means password has never been changed (first-login scenario).
    /// </summary>
    /// </summary>
    public DateTimeOffset? PasswordChangedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public Guid? ActiveSessionId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    // Names (backward compatible with existing API/UI)
    public List<string> Roles { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    // IDs (for JWT claims + integrations)
    public List<string> RoleIds { get; set; } = new();
    public long AuthzVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
