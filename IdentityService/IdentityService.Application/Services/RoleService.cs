using FluentValidation;
using IdentityService.Application.Common.Validation;
using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Roles;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Services;

public class RoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IValidator<CreateRoleRequestDto> _createValidator;
    private readonly IValidator<UpdateRoleRequestDto> _updateValidator;

    public RoleService(
        IRoleRepository roleRepository, 
        IPermissionRepository permissionRepository, 
        IUserRepository userRepository,
        IValidator<CreateRoleRequestDto> createValidator,
        IValidator<UpdateRoleRequestDto> updateValidator)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _userRepository = userRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _roleRepository.GetAllAsync(ct);
        return roles.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<RoleDto>> GetPagedAsync(PagedQuery query, CancellationToken ct = default)
    {
        query.Normalize();

        var (items, total) = await _roleRepository.SearchAsync(
            query.Search,
            query.SortBy,
            sortDesc: query.SortDirection == "desc",
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: ct);

        return new PagedResult<RoleDto>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total,
            Items = items.Select(MapToDto).ToList()
        };
    }

    public async Task<RoleDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, ct);
        return role is null ? null : MapToDto(role);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequestDto request, CancellationToken ct = default)
    {
        // Manual validation
        await _createValidator.ValidateAsync(request, ct);
        
        var existing = await _roleRepository.GetByNameAsync(request.Name, ct);
        if (existing is not null)
            throw new InvalidOperationException("Role name already exists.");

        foreach (var permName in request.Permissions.Distinct())
        {
            var perm = await _permissionRepository.GetByNameAsync(permName, ct);
            if (perm is null)
                throw new InvalidOperationException($"Permission '{permName}' does not exist.");
        }

        var role = new Role
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions.Distinct().ToList()
        };

        await _roleRepository.CreateAsync(role, ct);
        return MapToDto(role);
    }

    public async Task<RoleDto?> UpdateAsync(string id, UpdateRoleRequestDto request, CancellationToken ct = default)
    {
        // Manual validation
        await _updateValidator.ValidateAsync(request, ct);
        
        var role = await _roleRepository.GetByIdAsync(id, ct);
        if (role is null) return null;

        foreach (var permName in request.Permissions.Distinct())
        {
            var perm = await _permissionRepository.GetByNameAsync(permName, ct);
            if (perm is null)
                throw new InvalidOperationException($"Permission '{permName}' does not exist.");
        }

        role.Description = request.Description;
        role.Permissions = request.Permissions.Distinct().ToList();

        await _roleRepository.UpdateAsync(role, ct);
        await _userRepository.IncrementAuthzVersionForRoleAsync(role.Id, ct);
        return MapToDto(role);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, ct);
        if (role is null) return false;

        await _userRepository.IncrementAuthzVersionForRoleAsync(id, ct);
        await _roleRepository.DeleteAsync(id, ct);
        return true;
    }

    private static RoleDto MapToDto(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Permissions = role.Permissions
    };
}
