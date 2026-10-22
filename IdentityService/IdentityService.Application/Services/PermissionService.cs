using FluentValidation;
using IdentityService.Application.Common.Validation;
using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Permissions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Services;

public class PermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IValidator<CreatePermissionRequestDto> _createValidator;
    private readonly IValidator<UpdatePermissionRequestDto> _updateValidator;

    public PermissionService(
        IPermissionRepository permissionRepository, 
        IUserRepository userRepository,
        IValidator<CreatePermissionRequestDto> createValidator,
        IValidator<UpdatePermissionRequestDto> updateValidator)
    {
        _permissionRepository = permissionRepository;
        _userRepository = userRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _permissionRepository.GetAllAsync(ct);
        return items.Select(x => new PermissionDto
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description
        }).ToList();
    }

    public async Task<PagedResult<PermissionDto>> GetPagedAsync(PagedQuery query, CancellationToken ct = default)
    {
        query.Normalize();

        var (items, total) = await _permissionRepository.SearchAsync(
            query.Search,
            query.SortBy,
            sortDesc: query.SortDirection == "desc",
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: ct);

        return new PagedResult<PermissionDto>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total,
            Items = items.Select(MapToDto).ToList()
        };
    }

    public async Task<PermissionDto> CreateAsync(CreatePermissionRequestDto request, CancellationToken ct = default)
    {
        // Manual validation
        await _createValidator.ValidateAsync(request, ct);
        
        var existing = await _permissionRepository.GetByNameAsync(request.Name, ct);
        if (existing is not null)
            throw new InvalidOperationException("Permission already exists.");

        var perm = new Permission
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description
        };

        await _permissionRepository.CreateAsync(perm, ct);
        await _userRepository.IncrementAllAuthzVersionsAsync(ct);

        return new PermissionDto
        {
            Id = perm.Id,
            Name = perm.Name,
            Description = perm.Description
        };
    }

    public async Task<PermissionDto?> UpdateAsync(string id, UpdatePermissionRequestDto request, CancellationToken ct = default)
    {
        // Manual validation
        await _updateValidator.ValidateAsync(request, ct);
        
        var perm = await _permissionRepository.GetByIdAsync(id, ct);
        if (perm is null) return null;
        perm.Name = request.Name;
        perm.Description = request.Description;

        await _permissionRepository.UpdateAsync(perm, ct);
        await _userRepository.IncrementAllAuthzVersionsAsync(ct);
        return new PermissionDto
        {
            Id = perm.Id,
            Name = perm.Name,
            Description = perm.Description
        };
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var perm = await _permissionRepository.GetByIdAsync(id, ct);
        if (perm is null) return false;

        await _permissionRepository.DeleteAsync(id, ct);
        await _userRepository.IncrementAllAuthzVersionsAsync(ct);
        return true;
    }

    private static PermissionDto MapToDto(Permission permission) => new()
    {
        Id = permission.Id,
        Name = permission.Name,
        Description = permission.Description,
    };
}
