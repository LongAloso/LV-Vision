namespace IdentityService.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = default!;
    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; set; }
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// True when user must change password (first login / expired password policy).
    /// Frontend should redirect to change-password screen.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Informational: when password will expire (UTC). Null if unknown.
    /// </summary>
    public DateTimeOffset? PasswordExpiresAtUtc { get; set; }
}
