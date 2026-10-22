using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class UserDepartmentRepository : IUserDepartmentRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserDepartmentRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserDepartment>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            return Array.Empty<UserDepartment>();

        var entities = await _dbContext.UserDepartments
            .Where(ud => ud.UserId == guid)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<UserDepartment>> GetByDepartmentIdAsync(string departmentId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(departmentId, out var guid))
            return Array.Empty<UserDepartment>();

        var entities = await _dbContext.UserDepartments
            .Where(ud => ud.DepartmentId == guid)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AssignDepartmentsAsync(
        string userId,
        IReadOnlyList<(string DepartmentId, bool IsPrimary)> departments,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new InvalidOperationException("Invalid user id");

        var existing = await _dbContext.UserDepartments
            .Where(ud => ud.UserId == userGuid)
            .ToListAsync(cancellationToken);

        _dbContext.UserDepartments.RemoveRange(existing);

        if (departments == null || departments.Count == 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var docs = departments.Select(d => new UserDepartmentEntity
        {
            UserId = userGuid,
            DepartmentId = Guid.Parse(d.DepartmentId),
            IsPrimary = d.IsPrimary,
            AssignedAtUtc = DateTimeOffset.UtcNow
        }).ToList();

        await _dbContext.UserDepartments.AddRangeAsync(docs, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return;

        var existing = await _dbContext.UserDepartments
            .Where(ud => ud.UserId == userGuid)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0) return;

        _dbContext.UserDepartments.RemoveRange(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static UserDepartment MapToDomain(UserDepartmentEntity e) => new()
    {
        Id = $"{e.UserId:N}_{e.DepartmentId:N}",
        UserId = e.UserId.ToString("N"),
        DepartmentId = e.DepartmentId.ToString("N"),
        IsPrimary = e.IsPrimary,
        AssignedAtUtc = e.AssignedAtUtc
    };
}
