namespace IdentityService.Domain.Entities;

public class Role
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
