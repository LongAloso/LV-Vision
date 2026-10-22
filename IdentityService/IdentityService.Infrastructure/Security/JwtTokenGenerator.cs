using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Configuration;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public string GenerateAccessToken(User user, IEnumerable<string> permissions, DateTimeOffset expiresAtUtc, long? authzVersion = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("uid", user.Id),
            new("pwd_change_required", user.MustChangePassword ? "true" : "false"),
            new("authz_version", (authzVersion ?? user.AuthzVersion).ToString())
        };

        if (user.ActiveSessionId.HasValue)
            claims.Add(new Claim("session_id", user.ActiveSessionId.Value.ToString("D")));

        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var dept in user.Departments ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(dept))
                claims.Add(new Claim("department", dept));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
