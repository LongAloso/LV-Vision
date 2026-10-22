namespace IdentityService.Application.Security;

public interface IPermissionSnapshotService
{
    Task<PermissionSnapshot?> GetPermissionsAsync(string userId, long authzVersion, CancellationToken cancellationToken = default);
}
