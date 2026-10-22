using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Users;
using IdentityService.Application.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuthService _authService;
    private readonly IAuditEventPublisher _audit;

    public UsersController(UserService userService, AuthService authService, IAuditEventPublisher audit)
    {
        _userService = userService;
        _authService = authService;
        _audit = audit;
    }
    /// <summary>Danh sách user (phân trang + sort + search).</summary>
    [HttpGet]
    //[Authorize(Policy = SpcPermissions.Users.Read)]
    [Authorize]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] PagedQuery query,CancellationToken ct)
    {
        var result = await _userService.GetPagedAsync(query, ct);
        return Ok(result);
    }

    ///// <summary>Danh sách user (theo permission Users.Read).</summary>
    //[HttpGet]
    //[Authorize(Policy = "Users.Read")]
    //public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    //{
    //    var result = await _userService.GetPagedAsync(page, pageSize, ct);
    //    return Ok(result);
    //}

    /// <summary>Lấy user theo Id (Users.Read).</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = SpcPermissions.Users.Read)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        return Ok(user);
    }

    [HttpGet("internal/{id}")]
    [Authorize(Policy = "InternalService")]
    public async Task<IActionResult> GetByIdInternal(string id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        return Ok(user);
    }

    /// <summary>Tạo user mới (Users.Create).</summary>
    [HttpPost]
    [Authorize(Policy = SpcPermissions.Users.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var created = await _userService.CreateAsync(request, ct);
        await PublishAuditAsync("identity.user_created", "User created.", created.Id, ct,
            new Dictionary<string, object> { ["userName"] = created.UserName });
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Cập nhật user (Users.Update).</summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateUserRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var actorUserId = User.FindFirstValue("uid");

        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized();

        // Nếu sửa user khác → cần quyền Users.Manage
        if (actorUserId != id && !User.HasClaim("permission", SpcPermissions.Users.Manage))
        {
            return Forbid();
        }

        if (actorUserId != id)
        {
            var reauth = await RequireReauthAsync(reauthPassword, ct);
            if (reauth is not null) return reauth;
        }

        var updated = await _userService.UpdateAsync(id, request, actorUserId, ct);

        if (updated is null)
            return NotFound();

        await PublishAuditAsync("identity.user_updated", "User updated.", id, ct,
            new Dictionary<string, object> { ["selfUpdate"] = actorUserId == id });
        return Ok(updated);
    }

    /// <summary>
    /// Xóa user (Users.Delete). P3.5: requires re-authentication via
    /// X-Reauth-Password header — the actor's current password.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = SpcPermissions.Users.Manage)]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var deleted = await _userService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
        await PublishAuditAsync("identity.user_deleted", "User deleted.", id, ct, level: "Warning");
        return NoContent();
    }
    [HttpGet("lookup")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<UserLookupDto>>> GetLookups(CancellationToken ct)
    {
        return Ok(await _userService.GetUserLookupsAsync(ct));
    }

    private async Task<IActionResult?> RequireReauthAsync(string? reauthPassword, CancellationToken ct)
    {
        var actorUserId = User.FindFirstValue("uid");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Unauthorized();

        if (string.IsNullOrEmpty(reauthPassword))
            return Unauthorized(new { code = "reauth_required", message = "X-Reauth-Password header is required for this action." });

        var ok = await _authService.VerifyPasswordAsync(actorUserId, reauthPassword, ct);
        return ok ? null : Unauthorized(new { code = "reauth_failed", message = "Re-authentication failed." });
    }

    private Task PublishAuditAsync(
        string action,
        string message,
        string resourceId,
        CancellationToken ct,
        Dictionary<string, object>? data = null,
        string level = "Information")
    {
        return _audit.PublishAsync(
            action,
            message,
            level,
            userId: User.FindFirstValue("uid"),
            userName: User.Identity?.Name,
            resourceType: "User",
            resourceId: resourceId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString(),
            data: data,
            cancellationToken: ct);
    }
}
