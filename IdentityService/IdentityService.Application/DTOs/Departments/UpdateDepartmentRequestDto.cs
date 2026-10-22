namespace IdentityService.Application.DTOs.Departments;

public class UpdateDepartmentRequestDto
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? ParentId { get; set; }
    public bool IsActive { get; set; }
}
