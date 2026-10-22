namespace IdentityService.Application.Security;

public sealed class PermissionSnapshot
{
    public string UserId { get; init; } = default!;
    public long AuthzVersion { get; init; }
    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
}
