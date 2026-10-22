using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Roles;
using IdentityService.Application.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RoleService _roleService;
    private readonly AuthService _authService;
    private readonly IAuditEventPublisher _audit;

    public RolesController(RoleService roleService, AuthService authService, IAuditEventPublisher audit)
    {
        _roleService = roleService;
        _authService = authService;
        _audit = audit;
    }

    /// <summary>Danh s�ch role (ph�n trang + sort + search).</summary>
    [HttpGet]
    //[Authorize(Policy = SpcPermissions.Roles.Read)]
    [Authorize]
    public async Task<ActionResult<PagedResult<RoleDto>>> GetRoles([FromQuery] PagedQuery query,CancellationToken ct)
    {
        var result = await _roleService.GetPagedAsync(query, ct);
        return Ok(result);
    }

    //[HttpGet]
    //public async Task<IActionResult> GetAll(CancellationToken ct)
    //{
    //    var items = await _roleService.GetAllAsync(ct);
    //    return Ok(items);
    //}

    [HttpGet("{id}")]
    [Authorize(Policy = SpcPermissions.Roles.Read)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var role = await _roleService.GetByIdAsync(id, ct);
        if (role is null) return NotFound();
        return Ok(role);
    }

    [HttpPost]
    [Authorize(Policy = SpcPermissions.Roles.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var created = await _roleService.CreateAsync(request, ct);
        await PublishAuditAsync("identity.role_created", "Role created.", "Role", created.Id, ct,
            new Dictionary<string, object> { ["roleName"] = created.Name });
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = SpcPermissions.Roles.Manage)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateRoleRequestDto request,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var updated = await _roleService.UpdateAsync(id, request, ct);
        if (updated is null) return NotFound();
        await PublishAuditAsync("identity.role_updated", "Role updated.", "Role", id, ct,
            new Dictionary<string, object> { ["roleName"] = updated.Name });
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = SpcPermissions.Roles.Manage)]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "X-Reauth-Password")] string? reauthPassword,
        CancellationToken ct)
    {
        var reauth = await RequireReauthAsync(reauthPassword, ct);
        if (reauth is not null) return reauth;

        var ok = await _roleService.DeleteAsync(id, ct);
        if (!ok) return NotFound();
        await PublishAuditAsync("identity.role_deleted", "Role deleted.", "Role", id, ct, level: "Warning");
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
        string resourceType,
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
            resourceType: resourceType,
            resourceId: resourceId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString(),
            data: data,
            cancellationToken: ct);
    }
}
