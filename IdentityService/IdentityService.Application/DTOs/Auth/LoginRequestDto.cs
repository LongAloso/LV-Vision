namespace IdentityService.Application.DTOs.Auth;

public class LoginRequestDto
{
    /// <summary>
    /// Có thể là UserName hoặc Email
    /// </summary>
    public string UserNameOrEmail { get; set; } = default!;
    public string Password { get; set; } = default!;

    /// <summary>
    /// Nếu true, refresh token sẽ có thời gian sống dài hơn (Remember Me).
    /// </summary>
    public bool RememberMe { get; set; } = false;
}
