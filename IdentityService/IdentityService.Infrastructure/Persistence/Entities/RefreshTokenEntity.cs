using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityService.Infrastructure.Persistence.Entities;

[Table("refresh_tokens")]
public class RefreshTokenEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("token")]
    [MaxLength(500)]
    public string Token { get; set; } = default!;

    [Column("expires_at_utc")]
    public DateTimeOffset ExpiresAtUtc { get; set; }

    [Column("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Column("session_id")]
    public Guid? SessionId { get; set; }

    [Column("revoked_at_utc")]
    public DateTimeOffset? RevokedAtUtc { get; set; }

    [Column("replaced_by_token")]
    [MaxLength(500)]
    public string? ReplacedByToken { get; set; }

    public UserEntity User { get; set; } = default!;
}
