namespace IdentityService.Application.DTOs.Users;

public class UserDto
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool? DisplayDefault { get; set; } = default!;//By default, page displays in grid or list

    public byte? MeasurementType { get; set; } = default!;
    public byte? MeasurementDisplayMode { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }

    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> RoleIds { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    public long AuthzVersion { get; set; }
}
