using IdentityService.Application.DTOs.Auth;
using IdentityService.Application.Security;
using IdentityService.Application.Services;

namespace IdentityService.Api.Security;

public sealed class LocalIdentityPermissionClient : IIdentityPermissionClient
{
    private readonly AuthService _authService;

    public LocalIdentityPermissionClient(AuthService authService)
    {
        _authService = authService;
    }

    public Task<CurrentUserAuthzDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
        => _authService.GetCurrentUserAsync(userId, cancellationToken);
}
