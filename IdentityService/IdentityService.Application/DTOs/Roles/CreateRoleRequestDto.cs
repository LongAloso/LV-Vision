namespace IdentityService.Application.DTOs.Roles;

public class CreateRoleRequestDto
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
