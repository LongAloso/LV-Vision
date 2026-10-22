using FluentValidation;
using IdentityService.Application.Common.Validation;
using IdentityService.Application.DTOs.Common;
using IdentityService.Application.DTOs.Users;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Services;

public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IValidator<CreateUserRequestDto> _createValidator;
    private readonly IValidator<UpdateUserRequestDto> _updateValidator;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IDepartmentRepository departmentRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IValidator<CreateUserRequestDto> createValidator,
        IValidator<UpdateUserRequestDto> updateValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _departmentRepository = departmentRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<UserDto>> GetPagedAsync(PagedQuery query, CancellationToken ct = default)
    {
        query.Normalize();

        var (items, total) = await _userRepository.SearchAsync(
            query.Search,
            query.SortBy,
            sortDesc: query.SortDirection == "desc",
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: ct);

        return new PagedResult<UserDto>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total,
            Items = items.Select(MapToDto).ToList()
        };
    }

    public async Task<UserDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        // Manual validation
        await _createValidator.ValidateAsync(request, cancellationToken);
        
        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = request.UserName,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Roles = request.Roles,
            Departments = request.Departments,
            DisplayDefault = true,
            MeasurementDisplayMode = 0,
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAtUtc = DateTimeOffset.UtcNow,
            PhoneNumber = request.PhoneNumber,
            ProfileImageUrl = request.ProfileImageUrl,
            AuthzVersion = 1
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        await AssignDepartmentsInternalAsync(user.Id, request.Departments, cancellationToken);

        var reloaded = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
        return MapToDto(reloaded ?? user);
    }

    public async Task<UserDto?> UpdateAsync(string id, UpdateUserRequestDto request, string? actorUserId = null, CancellationToken cancellationToken = default)
    {
        // Manual validation
        await _updateValidator.ValidateAsync(request, cancellationToken);
        
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null) return null;

        var oldRoles = (user.Roles ?? new List<string>()).ToList();
        var oldDepartments = (user.Departments ?? new List<string>()).ToList();

        user.FullName = request.FullName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        if (actorUserId != id)
        {
            user.Roles = request.Roles;
        }
        user.DisplayDefault = request.DisplayDefault ?? user.DisplayDefault;
        user.MeasurementType = request.MeasurementType ?? user.MeasurementType;
        user.MeasurementDisplayMode = request.MeasurementDisplayMode ?? user.MeasurementDisplayMode;
        user.PhoneNumber = request.PhoneNumber;
        user.ProfileImageUrl = request.ProfileImageUrl;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (string.Equals(request.Password, user.UserName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Password must not match the username.");

            user.PasswordHash = _passwordHasher.HashPassword(request.Password);
            user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
            user.AccessFailedCount = 0;
            user.LockoutEnabled = false;
            user.LockoutEndUtc = null;
            user.MustChangePassword = !string.IsNullOrWhiteSpace(actorUserId)
                && !string.Equals(actorUserId, user.Id, StringComparison.OrdinalIgnoreCase);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        if (actorUserId != id)
        {
            await AssignDepartmentsInternalAsync(user.Id, request.Departments, cancellationToken);
        }

        var rolesChanged = actorUserId != id && !oldRoles.SequenceEqual(request.Roles ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var departmentsChanged = actorUserId != id && !oldDepartments.SequenceEqual(request.Departments ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        if (rolesChanged || departmentsChanged)
        {
            await _userRepository.IncrementAuthzVersionAsync(user.Id, cancellationToken);
        }

        var reloaded = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
        return MapToDto(reloaded ?? user);
    }

    private async Task AssignDepartmentsInternalAsync(string userId, List<string> departmentIds, CancellationToken ct)
    {
        if (departmentIds == null || departmentIds.Count == 0)
        {
            await _userDepartmentRepository.RemoveAllForUserAsync(userId, ct);
            return;
        }

        var allDeps = await _departmentRepository.GetAllAsync(ct);
        var valid = new HashSet<string>(allDeps.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);

        var normalized = departmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => valid.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            await _userDepartmentRepository.RemoveAllForUserAsync(userId, ct);
            return;
        }

        var input = normalized
            .Select((deptId, idx) => (DepartmentId: deptId, IsPrimary: idx == 0))
            .ToList()
            .AsReadOnly();

        await _userDepartmentRepository.AssignDepartmentsAsync(userId, input, ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null) return false;

        await _userRepository.DeleteAsync(id, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserLookupDto>> GetUserLookupsAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetBasicUsersAsync(ct);
        return users.Select(u => new UserLookupDto
        {
            Id = u.Id,
            UserName = u.UserName,
            FullName = u.FullName
        }).ToList();
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        FullName = user.FullName,
        Email = user.Email,
        DisplayDefault = user.DisplayDefault,
        MeasurementType = user.MeasurementType,
        MeasurementDisplayMode = user.MeasurementDisplayMode,
        PhoneNumber = user.PhoneNumber,
        ProfileImageUrl = user.ProfileImageUrl,
        IsActive = user.IsActive,
        Roles = user.Roles,
        RoleIds = user.RoleIds,
        Departments = user.Departments,
        AuthzVersion = user.AuthzVersion
    };
}
