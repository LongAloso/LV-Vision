namespace IdentityService.Application.DTOs.Users;

public class UpdateUserRequestDto
{
    public string Email { get; set; } = default!;
    public string? Password { get; set; }
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool? DisplayDefault { get; set; } = default!;//By default, page displays in grid or list

    public byte? MeasurementType { get; set; } = default!;
    public byte? MeasurementDisplayMode { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }

    public List<string> Roles { get; set; } = new();
    public List<string> Departments { get; set; } = new();
}
