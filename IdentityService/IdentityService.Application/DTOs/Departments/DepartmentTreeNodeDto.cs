namespace IdentityService.Application.DTOs.Departments;

public class DepartmentTreeNodeDto
{
    public string Id { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ParentId { get; set; }
    public bool IsActive { get; set; }

    public List<DepartmentTreeNodeDto> Children { get; set; } = new();
}
