using IdentityService.Application.DTOs.Auth;

namespace IdentityService.Application.Security;

public interface IIdentityPermissionClient
{
    Task<CurrentUserAuthzDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
