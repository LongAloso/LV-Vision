namespace IdentityService.Domain.Entities;

public class UserDepartment
{
    public string Id { get; set; } = default!; // composite key encoded as string
    public string UserId { get; set; } = default!;
    public string DepartmentId { get; set; } = default!;
    public bool IsPrimary { get; set; } = false;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
