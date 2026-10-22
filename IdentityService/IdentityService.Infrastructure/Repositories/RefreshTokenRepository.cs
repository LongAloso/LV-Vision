using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        var entity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(refreshToken.UserId),
            Token = refreshToken.Token,
            ExpiresAtUtc = refreshToken.ExpiresAtUtc,
            CreatedAtUtc = refreshToken.CreatedAtUtc,
            SessionId = refreshToken.SessionId,
            RevokedAtUtc = refreshToken.RevokedAtUtc
        };

        await _dbContext.RefreshTokens.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        refreshToken.Id = entity.Id.ToString("N");
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(refreshToken.Id, out var guid))
            throw new InvalidOperationException("Invalid refresh token id");

        var entity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Refresh token not found");

        entity.Token = refreshToken.Token;
        entity.ExpiresAtUtc = refreshToken.ExpiresAtUtc;
        entity.CreatedAtUtc = refreshToken.CreatedAtUtc;
        entity.SessionId = refreshToken.SessionId;
        entity.RevokedAtUtc = refreshToken.RevokedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeByUserIdAsync(string userId, DateTimeOffset revokedAtUtc, Guid? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var guid))
            throw new InvalidOperationException("Invalid user id");

        var query = _dbContext.RefreshTokens
            .Where(x => x.UserId == guid && x.RevokedAtUtc == null && x.ExpiresAtUtc > revokedAtUtc);

        if (exceptSessionId.HasValue)
            query = query.Where(x => x.SessionId != exceptSessionId);

        await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.RevokedAtUtc, _ => revokedAtUtc), cancellationToken);
    }

    private static RefreshToken MapToDomain(RefreshTokenEntity entity)
    {
        return new RefreshToken
        {
            Id = entity.Id.ToString("N"),
            UserId = entity.UserId.ToString("N"),
            Token = entity.Token,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            SessionId = entity.SessionId,
            RevokedAtUtc = entity.RevokedAtUtc
        };
    }
}
