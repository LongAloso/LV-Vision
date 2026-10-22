namespace IdentityService.Application.DTOs.Auth;

public sealed class CurrentUserAuthzDto
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool? DisplayDefault { get; set; }
    public byte? MeasurementType { get; set; }
    public byte? MeasurementDisplayMode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool MustChangePassword { get; set; }
    public long AuthzVersion { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> RoleIds { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}
