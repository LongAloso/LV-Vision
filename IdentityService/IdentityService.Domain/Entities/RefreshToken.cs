namespace IdentityService.Domain.Entities;

public class RefreshToken
{
    public string Id { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? SessionId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
}
