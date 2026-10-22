namespace IdentityService.Application.DTOs.Departments;

public class UserDepartmentDto
{
    public string DepartmentId { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
}
