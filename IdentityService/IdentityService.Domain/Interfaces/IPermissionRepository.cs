using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Permission?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(Permission permission, CancellationToken cancellationToken = default);
    Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Permission> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
