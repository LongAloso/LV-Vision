namespace IdentityService.Application.DTOs.Auth;

public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = default!;
}
