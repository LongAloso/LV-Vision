using IdentityService.Application.Security;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityService.Api.Security;

public sealed class MemoryPermissionSnapshotService : IPermissionSnapshotService
{
    internal static readonly TimeSpan CurrentUserCacheTtl = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;
    private readonly IIdentityPermissionClient _identityPermissionClient;
    private readonly ILogger<MemoryPermissionSnapshotService> _logger;

    public MemoryPermissionSnapshotService(
        IMemoryCache cache,
        IIdentityPermissionClient identityPermissionClient,
        ILogger<MemoryPermissionSnapshotService> logger)
    {
        _cache = cache;
        _identityPermissionClient = identityPermissionClient;
        _logger = logger;
    }

    public async Task<PermissionSnapshot?> GetPermissionsAsync(string userId, long authzVersion, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"authz:perm:{userId}:v{authzVersion}";

        if (_cache.TryGetValue(cacheKey, out PermissionSnapshot? snapshot) && snapshot is not null)
            return snapshot;

        var me = await _identityPermissionClient.GetCurrentUserAsync(userId, cancellationToken);
        if (me is null)
            return null;

        if (me.AuthzVersion != authzVersion)
        {
            _logger.LogWarning(
                "Authz version mismatch for user {UserId}. Token={TokenVersion}, Current={CurrentVersion}",
                userId,
                authzVersion,
                me.AuthzVersion);
            return null;
        }

        // The authenticated /me request needs the same DTO immediately after
        // this validation step. Reuse the version-bound result instead of
        // issuing a second identical user/role/permission query in the
        // controller. Keep this shorter than the permission snapshot cache so
        // non-authorization profile changes remain promptly visible.
        _cache.Set(
            GetCurrentUserCacheKey(userId, authzVersion),
            me,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CurrentUserCacheTtl,
                Size = 1
            });

        snapshot = new PermissionSnapshot
        {
            UserId = me.UserId,
            AuthzVersion = me.AuthzVersion,
            Permissions = me.Permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        _cache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Size = 1
        });

        return snapshot;
    }

    internal static string GetCurrentUserCacheKey(string userId, long authzVersion)
        => $"authz:me:{userId}:v{authzVersion}";
}
