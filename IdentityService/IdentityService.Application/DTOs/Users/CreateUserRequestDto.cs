namespace IdentityService.Application.DTOs.Users;

public class CreateUserRequestDto
{
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public List<string> Roles { get; set; } = new();
    public List<string> Departments { get; set; } = new();

    public string ? PhoneNumber { get; set;} 
    public string? ProfileImageUrl { get; set; }

}
