using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Roles;
using IdentityService.Application.DTOs.UserSettings;
using IdentityService.Application.Services;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/user-settings")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly UserSettingService _userSettingService;
    public UserSettingsController(UserSettingService userSettingService)
    {
        _userSettingService = userSettingService;
    }

    // 1. Lấy danh sách settings (Search)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSettingDto>>> GetAll([FromQuery] UserSettingsRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        request.UserId = userId.Value.ToString();
        var result = await _userSettingService.GetAllAsync(request, ct);
        return Ok(result);
    }

    // 2. Tạo mới setting
    [HttpPost]
    public async Task<ActionResult<UserSettingDto>> Create([FromBody] CreateUserSettingRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        request.UserId = userId.Value;
        request.UserName = null;
        var result = await _userSettingService.CreateAsync(request, ct);
            // Trả về 201 Created kèm theo object vừa tạo
            return CreatedAtAction(nameof(GetAll), new { userId = result.UserId }, result);   
    }

    // 3. Cập nhật setting
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserSettingDto>> Update(Guid id, [FromBody] UpdateUserSettingRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        UserSettingDto? result;
        try
        {
            result = await _userSettingService.UpdateAsync(id, request, userId.Value, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    // 4. Xóa setting
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        bool success;
        try
        {
            success = await _userSettingService.DeleteAsync(id, userId.Value, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (!success)
            return NotFound();

        return NoContent(); // Trả về 204 nếu xóa thành công
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue("uid");
        return Guid.TryParse(userId, out var parsed) ? parsed : null;
    }
}
