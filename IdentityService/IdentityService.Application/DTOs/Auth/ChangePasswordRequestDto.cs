namespace IdentityService.Application.DTOs.Auth;

public sealed class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
}
