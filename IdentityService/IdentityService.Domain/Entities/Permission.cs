namespace IdentityService.Domain.Entities;

public class Permission
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
