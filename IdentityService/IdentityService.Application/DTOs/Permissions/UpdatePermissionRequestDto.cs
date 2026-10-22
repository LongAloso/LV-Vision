namespace IdentityService.Application.DTOs.Permissions;

public class UpdatePermissionRequestDto
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
