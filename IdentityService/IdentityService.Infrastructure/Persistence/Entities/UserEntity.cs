using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_name")]
    [MaxLength(100)]
    public string UserName { get; set; } = default!;

    [Column("full_name")]
    [MaxLength(100)]
    public string FullName { get; set; } = default!;

    [Column("email")]
    [MaxLength(200)]
    public string Email { get; set; } = default!;
    [Column("display_default")]
    public bool? DisplayDefault { get; set; }

    [Column("measurement_type")]
    public byte? MeasurementType { get; set; } = default!;

    [Column("measurement_display_mode")]
    public byte? MeasurementDisplayMode { get; set; } = default!;

    [Column("normalized_email")]
    [MaxLength(200)]
    public string NormalizedEmail { get; set; } = default!;

    [Column("password_hash")]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = default!;

    [Column("password_salt")]
    [MaxLength(200)]
    public string? PasswordSalt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("lockout_enabled")]
    public bool LockoutEnabled { get; set; }

    [Column("lockout_end_utc")]
    public DateTimeOffset? LockoutEndUtc { get; set; }

    [Column("access_failed_count")]
    public int AccessFailedCount { get; set; }

    [Column("must_change_password")]
    public bool MustChangePassword { get; set; }

    [Column("password_changed_at_utc")]
    public DateTimeOffset? PasswordChangedAtUtc { get; set; }

    [Column("last_login_at_utc")]
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    [Column("active_session_id")]
    public Guid? ActiveSessionId { get; set; }

    [Column("phone_number")]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [Column("profile_image_url")]
    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    [Column("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [Column("authz_version")]
    public long AuthzVersion { get; set; } = 1;

    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    public ICollection<UserDepartmentEntity> UserDepartments { get; set; } = new List<UserDepartmentEntity>();
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}
