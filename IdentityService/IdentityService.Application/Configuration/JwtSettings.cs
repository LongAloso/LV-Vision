namespace IdentityService.Application.Configuration;

public class JwtSettings
{
    public string Key { get; set; } = "dev-super-secret-key-1234567890!!";
    public string Issuer { get; set; } = "IdentityServiceDev";
    public string Audience { get; set; } = "IdentityClientsDev";

    /// <summary>
    /// Thời gian sống của access token (phút).
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>
    /// Thời gian sống của refresh token (ngày) cho đăng nhập bình thường.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Thời gian sống của refresh token (ngày) khi user chọn Remember Me.
    /// </summary>
    public int RefreshTokenRememberMeDays { get; set; } = 30;
}
