namespace IdentityService.Application.DTOs.Roles;

public class UpdateRoleRequestDto
{
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
