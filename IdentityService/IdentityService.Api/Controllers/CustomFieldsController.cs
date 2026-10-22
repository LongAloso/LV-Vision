using IdentityService.Application.DTOs.CustomFields;
using IdentityService.Application.Services;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/custom-fields")]
public class CustomFieldsController : ControllerBase
{
    private readonly CustomFieldService _service;

    public CustomFieldsController(CustomFieldService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get all definitions. Admin only.
    /// Optional filter: ?entityType=measurement_requests|parts|gages
    /// </summary>
    [HttpGet]
    [Authorize(Policy = SpcPermissions.CustomFields.Read)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> GetAllAsync([FromQuery] string? entityType, CancellationToken ct)
        => Ok(await _service.GetAllAsync(entityType, ct));

    /// <summary>
    /// Get ACTIVE definitions for a target entity type.
    /// Used by other services (e.g., MeasurementService) and UI forms.
    /// </summary>
    [HttpGet("active/{entityType}")]
    //[Authorize(Policy = SpcPermissions.CustomFields.Read)]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ActiveCustomFieldDefinitionDto>>> GetActiveAsync([FromRoute] string entityType, CancellationToken ct)
        => Ok(await _service.GetActiveAsync(entityType, ct));

    /// <summary>
    /// Create a new custom field definition. Admin only.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = SpcPermissions.CustomFields.Manage)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> CreateAsync([FromBody] CreateCustomFieldRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return Ok(created);
    }

    /// <summary>
    /// Update a definition. Admin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = SpcPermissions.CustomFields.Manage)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> UpdateAsync(Guid id, [FromBody] UpdateCustomFieldRequest req, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, req, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Deactivate (soft delete) a definition. Admin only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SpcPermissions.CustomFields.Manage)]
    public async Task<IActionResult> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
