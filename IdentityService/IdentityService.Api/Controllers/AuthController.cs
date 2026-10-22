using System.Net;
using IdentityService.Api.Security;
using IdentityService.Application.DTOs.Auth;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IMemoryCache _cache;

    public AuthController(AuthService authService, IMemoryCache cache)
    {
        _authService = authService;
        _cache = cache;
    }

    [HttpGet("debug/claims")]
    public IActionResult DebugClaims()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(claims);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthResponseDto), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequestDto request,
        [FromQuery] bool revokeAll = false,
        CancellationToken ct = default)
    {
        // P3.3: pass revokeAll through so client can choose
        // "logout this device" (default) or "logout everywhere".
        await _authService.RevokeRefreshTokenAsync(request, revokeAll, ct);
        return NoContent();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequestDto request, CancellationToken ct)
    {
        var user = await _authService.RegisterAsync(request, ct);
        return Ok(new { user.Id, user.UserName, user.Email });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("uid");
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserAuthzDto), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue("uid");
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var hasVersion = long.TryParse(User.FindFirstValue("authz_version"), out var authzVersion)
                         && authzVersion > 0;
        if (hasVersion
            && _cache.TryGetValue(
                MemoryPermissionSnapshotService.GetCurrentUserCacheKey(userId, authzVersion),
                out CurrentUserAuthzDto? cached)
            && cached is not null)
        {
            return Ok(cached);
        }

        var me = await _authService.GetCurrentUserAsync(userId, ct);
        if (me is null)
            return NotFound();

        if (hasVersion && me.AuthzVersion != authzVersion)
            return Unauthorized();

        if (hasVersion)
        {
            _cache.Set(
                MemoryPermissionSnapshotService.GetCurrentUserCacheKey(userId, authzVersion),
                me,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = MemoryPermissionSnapshotService.CurrentUserCacheTtl,
                    Size = 1
                });
        }

        return Ok(me);
    }
}
