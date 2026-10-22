using System;
using System.Collections.Generic;
using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generate access token for a user with attached permissions.
    /// </summary>
    string GenerateAccessToken(User user, IEnumerable<string> permissions, DateTimeOffset expiresAtUtc, long? authzVersion = null);
}
