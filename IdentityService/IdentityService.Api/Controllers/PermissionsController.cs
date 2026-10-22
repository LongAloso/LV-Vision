using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Permissions;
using IdentityService.Application.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly PermissionService _permissionService;
    private readonly AuthService _authService;
    private readonly IAuditEventPublisher _audit;

    public PermissionsController(PermissionService permissionService, AuthService authService, IAuditEventPublisher audit)
    {
        _permissionService = permissionService;
        _authService = authService;
        _audit = audit;
    }

    /// <summary>Danh s�ch permission (ph�n trang + sort + search).</summary>
    [HttpGet]
    [Authorize(Policy = SpcPermissions.Permissions.Read)]
    public async Task<ActionResult<PagedResult<PermissionDto>>> GetPermissions([FromQuery] PagedQuery query,CancellationToken ct)
    {
        var result = await _permissionService.GetPagedAsync(query, ct);
        return Ok(result);
    }

    //[HttpGet]
    //public async Task<IActionResult> GetAll(CancellationToken ct)
    //{
    //    var items = await _permissionService.GetAllAsync(ct);
    //    return Ok(items);
    //}

    [HttpPost]
    [Authorize(Policy = SpcPermissions.Permissions.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePermissionRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var created = await _permissionService.CreateAsync(request, ct);
        await PublishAuditAsync("identity.permission_created", "Permission created.", created.Id, ct,
            new Dictionary<string, object> { ["permissionName"] = created.Name });
        return CreatedAtAction(nameof(GetPermissions), created);
    }
    
    [HttpPut("{id}")]
    [Authorize(Policy = SpcPermissions.Permissions.Manage)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdatePermissionRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var updated = await _permissionService.UpdateAsync(id, request, ct);
        if (updated is null) return NotFound();
        await PublishAuditAsync("identity.permission_updated", "Permission updated.", id, ct,
            new Dictionary<string, object> { ["permissionName"] = updated.Name });
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = SpcPermissions.Permissions.Manage)]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var ok = await _permissionService.DeleteAsync(id, ct);
        if (!ok) return NotFound();
        await PublishAuditAsync("identity.permission_deleted", "Permission deleted.", id, ct, level: "Warning");
        return NoContent();
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
            resourceType: "Permission",
            resourceId: resourceId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString(),
            data: data,
            cancellationToken: ct);
    }
}
