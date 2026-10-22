using FluentValidation;
using IdentityService.Application.Common.Validation;
using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Departments;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Services;

public class DepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IValidator<CreateDepartmentRequestDto> _createValidator;
    private readonly IValidator<UpdateDepartmentRequestDto> _updateValidator;

    public DepartmentService(
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IValidator<CreateDepartmentRequestDto> createValidator,
        IValidator<UpdateDepartmentRequestDto> updateValidator)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    #region Departments CRUD

    public async Task<PagedResult<DepartmentDto>> GetPagedAsync(PagedQuery query, CancellationToken ct = default)
    {
        query.Normalize();

        var (items, total) = await _departmentRepository.SearchAsync(
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.Page,
            query.PageSize,
            ct);

        return new PagedResult<DepartmentDto>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total,
            Items = items.Select(MapToDto).ToList()
        };
    }

    public async Task<IReadOnlyList<DepartmentTreeNodeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _departmentRepository.GetAllAsync(ct);
        var dict = all.ToDictionary(x => x.Id, MapToTreeNode);

        List<DepartmentTreeNodeDto> roots = new();

        foreach (var node in dict.Values)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentId) && dict.TryGetValue(node.ParentId!, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    public async Task<DepartmentDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var entity = await _departmentRepository.GetByIdAsync(id, ct);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<string> CreateAsync(CreateDepartmentRequestDto dto, CancellationToken ct = default)
    {
        // Manual validation
        await _createValidator.ValidateAsync(dto, ct);
        
        var existing = await _departmentRepository.GetByCodeAsync(dto.Code, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Department code '{dto.Code}' already exists.");

        var entity = new Department
        {
            Id = Guid.NewGuid().ToString("N"),
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ParentId = dto.ParentId,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _departmentRepository.CreateAsync(entity, ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(string id, UpdateDepartmentRequestDto dto, CancellationToken ct = default)
    {
        // Manual validation
        await _updateValidator.ValidateAsync(dto, ct);
        
        var entity = await _departmentRepository.GetByIdAsync(id, ct);
        if (entity is null) return false;
        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.ParentId = dto.ParentId;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _departmentRepository.UpdateAsync(entity, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _departmentRepository.GetByIdAsync(id, ct);
        if (entity is null) return false;

        // TODO: optional - check if any users belong to this department before deleting
        await _departmentRepository.DeleteAsync(id, ct);
        return true;
    }

    #endregion

    #region User Departments

    public async Task<IReadOnlyList<UserDepartmentDto>> GetUserDepartmentsAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null) return Array.Empty<UserDepartmentDto>();

        var userDeps = await _userDepartmentRepository.GetByUserIdAsync(userId, ct);
        if (userDeps.Count == 0) return Array.Empty<UserDepartmentDto>();

        var allDeps = await _departmentRepository.GetAllAsync(ct);
        var deptDict = allDeps.ToDictionary(d => d.Id, d => d);

        return userDeps
            .Where(ud => deptDict.ContainsKey(ud.DepartmentId))
            .Select(ud =>
            {
                var dep = deptDict[ud.DepartmentId];
                return new UserDepartmentDto
                {
                    DepartmentId = dep.Id,
                    Code = dep.Code,
                    Name = dep.Name,
                    IsPrimary = ud.IsPrimary,
                    AssignedAtUtc = ud.AssignedAtUtc
                };
            })
            .ToList();
    }

    public async Task<bool> AssignUserDepartmentsAsync(AssignUserDepartmentsRequestDto request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null) return false;

        var allDeps = await _departmentRepository.GetAllAsync(ct);
        var validIds = new HashSet<string>(allDeps.Select(d => d.Id));

        var normalized = request.Departments
            .Where(d => validIds.Contains(d.DepartmentId))
            .ToList();

        if (normalized.Count == 0)
        {
            await _userDepartmentRepository.RemoveAllForUserAsync(user.Id, ct);
            return true;
        }

        if (!normalized.Any(d => d.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }
        else
        {
            bool seen = false;
            foreach (var item in normalized)
            {
                if (item.IsPrimary)
                {
                    if (!seen) { seen = true; }
                    else item.IsPrimary = false;
                }
            }
        }

        var input = normalized
            .Select(d => (d.DepartmentId, d.IsPrimary))
            .ToList()
            .AsReadOnly();

        await _userDepartmentRepository.AssignDepartmentsAsync(user.Id, input, ct);
        return true;
    }

    #endregion

    #region Mapping

    private static DepartmentDto MapToDto(Department d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        Description = d.Description,
        ParentId = d.ParentId,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc
    };

    private static DepartmentTreeNodeDto MapToTreeNode(Department d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        ParentId = d.ParentId,
        IsActive = d.IsActive
    };

    #endregion
}
