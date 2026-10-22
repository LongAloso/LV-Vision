using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<Guid?> GetUserIdByUserNameAsync(string userName, CancellationToken cancellationToken = default); 
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken = default);
    Task CreateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update security-related fields without touching profile fields.
    /// </summary>
    Task UpdateSecurityStateAsync(
        string userId,
        int accessFailedCount,
        bool lockoutEnabled,
        DateTimeOffset? lockoutEndUtc,
        bool mustChangePassword,
        DateTimeOffset? passwordChangedAtUtc,
        DateTimeOffset? lastLoginAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update password hash and reset lockout/failed counters.
    /// </summary>
    Task UpdatePasswordAsync(
        string userId,
        string newPasswordHash,
        DateTimeOffset passwordChangedAtUtc,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, long TotalItems)> SearchAsync(
        string? search,
        string? sortBy,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetBasicUsersAsync(CancellationToken ct = default);
    Task<Guid?> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateActiveSessionAsync(string userId, Guid? activeSessionId, DateTimeOffset? lastLoginAtUtc, CancellationToken cancellationToken = default);
    Task IncrementAuthzVersionAsync(string userId, CancellationToken cancellationToken = default);
    Task IncrementAuthzVersionForRoleAsync(string roleId, CancellationToken cancellationToken = default);
    Task IncrementAllAuthzVersionsAsync(CancellationToken cancellationToken = default);
}
