namespace IdentityService.Application.DTOs.Permissions;

public class PermissionDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
