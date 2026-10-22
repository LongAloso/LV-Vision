using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Department> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        string? sortDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
