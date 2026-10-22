using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Departments;
using IdentityService.Application.Services;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : ControllerBase
{
    private readonly DepartmentService _departmentService;

    public DepartmentsController(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>Danh sách phòng ban (phân trang + sort + search).</summary>
    [HttpGet]
    //[Authorize(Policy = SpcPermissions.Departments.Read)]
    [Authorize]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> GetDepartments([FromQuery] PagedQuery query, CancellationToken ct)
    {
        var result = await _departmentService.GetPagedAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Lấy cây phòng ban.</summary>
    [HttpGet("tree")]
    [Authorize(Policy = SpcPermissions.Departments.Read)]
    public async Task<ActionResult<IReadOnlyList<DepartmentTreeNodeDto>>> GetTree(CancellationToken ct)
    {
        var result = await _departmentService.GetTreeAsync(ct);
        return Ok(result);
    }

    /// <summary>Chi tiết 1 phòng ban.</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = SpcPermissions.Departments.Read)]
    public async Task<ActionResult<DepartmentDto>> GetById(string id, CancellationToken ct)
    {
        var dto = await _departmentService.GetByIdAsync(id, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    /// <summary>Tạo phòng ban mới.</summary>
    [HttpPost]
    [Authorize(Policy = SpcPermissions.Departments.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequestDto dto, CancellationToken ct)
    {
        var id = await _departmentService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>Cập nhật phòng ban.</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = SpcPermissions.Departments.Manage)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDepartmentRequestDto dto, CancellationToken ct)
    {
        var ok = await _departmentService.UpdateAsync(id, dto, ct);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Xoá phòng ban.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = SpcPermissions.Departments.Manage)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var ok = await _departmentService.DeleteAsync(id, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}
