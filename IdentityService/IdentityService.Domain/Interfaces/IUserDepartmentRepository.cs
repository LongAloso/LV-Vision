using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IUserDepartmentRepository
{
    Task<IReadOnlyList<UserDepartment>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDepartment>> GetByDepartmentIdAsync(string departmentId, CancellationToken cancellationToken = default);

    Task AssignDepartmentsAsync(
        string userId,
        IReadOnlyList<(string DepartmentId, bool IsPrimary)> departments,
        CancellationToken cancellationToken = default);

    Task RemoveAllForUserAsync(string userId, CancellationToken cancellationToken = default);
}
