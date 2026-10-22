namespace IdentityService.Application.Configuration;

/// <summary>
/// Security policy settings (password policy, lockout, expiration).
/// Bound from configuration section: "Security".
/// </summary>
public sealed class SecuritySettings
{
    public int PasswordMinLength { get; set; } = 8;
    public int PasswordExpiryDays { get; set; } = 90;
    public int MaxFailedAccessAttempts { get; set; } = 10;
    public int LockoutMinutes { get; set; } = 30;

    /// <summary>
    /// When true, a user can only keep one active client session at a time.
    /// New login rotates the active session and older clients are forced to logout.
    /// </summary>
    public bool SingleClientLoginEnabled { get; set; } = false;
}
