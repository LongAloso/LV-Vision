namespace IdentityService.Application.DTOs.Departments;

public class AssignUserDepartmentsRequestDto
{
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Danh sách phòng ban user thuộc về.
    /// Chỉ nên có duy nhất 1 item IsPrimary = true.
    /// </summary>
    public List<AssignUserDepartmentItemDto> Departments { get; set; } = new();
}

public class AssignUserDepartmentItemDto
{
    public string DepartmentId { get; set; } = default!;
    public bool IsPrimary { get; set; }
}
