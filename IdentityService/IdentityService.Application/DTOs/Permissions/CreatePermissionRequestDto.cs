namespace IdentityService.Application.DTOs.Permissions;

public class CreatePermissionRequestDto
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
