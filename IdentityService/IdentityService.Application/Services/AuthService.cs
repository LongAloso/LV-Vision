using FluentValidation;
using IdentityService.Application.Common.Validation;
using IdentityService.Application.Configuration;
using IdentityService.Application.DTOs.Auth;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRoleRepository _roleRepository;
    private readonly JwtSettings _jwtSettings;
    private readonly SecuritySettings _security;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;
    private readonly IValidator<RegisterUserRequestDto> _registerValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;
    private readonly IAuditEventPublisher _audit;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IRoleRepository roleRepository,
        IOptions<JwtSettings> jwtOptions,
        IOptions<SecuritySettings> securityOptions,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator,
        IValidator<RegisterUserRequestDto> registerValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IAuditEventPublisher audit)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _roleRepository = roleRepository;
        _jwtSettings = jwtOptions.Value;
        _security = securityOptions.Value;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _registerValidator = registerValidator;
        _changePasswordValidator = changePasswordValidator;
        _audit = audit;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // P3.2: ValidateAndThrowAsync makes a failed validation surface as ValidationException
        // (caught by ExceptionHandlingMiddleware → 400). Previously ValidateAsync only
        // produced a ValidationResult that was silently discarded.
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);
        
        var identifier = request.UserNameOrEmail;
        var user = await _userRepository.GetByUserNameOrEmailAsync(identifier, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await _audit.PublishAsync(
                action: "auth.login_failed",
                message: $"Login failed for identifier '{identifier}': user not found or inactive.",
                level: "Warning",
                userName: identifier,
                cancellationToken: cancellationToken);
            throw new UnauthorizedAccessException("Invalid username/email or password.");
        }

        if (user.LockoutEnabled && user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTimeOffset.UtcNow)
        {
            await _audit.PublishAsync(
                action: "auth.login_blocked",
                message: $"Login blocked for user '{user.UserName}': account locked.",
                level: "Warning",
                userId: user.Id,
                userName: user.UserName,
                cancellationToken: cancellationToken);
            throw new UnauthorizedAccessException("Account is locked. Please try again later.");
        }

        if (!_passwordHasher.VerifyHashedPassword(user.PasswordHash, request.Password))
        {
            var failed = user.AccessFailedCount + 1;
            var lockoutEnabled = user.LockoutEnabled;
            DateTimeOffset? lockoutEnd = user.LockoutEndUtc;

            if (failed >= _security.MaxFailedAccessAttempts)
            {
                lockoutEnabled = true;
                lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(_security.LockoutMinutes);
            }

            await _userRepository.UpdateSecurityStateAsync(
                user.Id,
                accessFailedCount: failed,
                lockoutEnabled: lockoutEnabled,
                lockoutEndUtc: lockoutEnd,
                mustChangePassword: user.MustChangePassword,
                passwordChangedAtUtc: user.PasswordChangedAtUtc,
                lastLoginAtUtc: user.LastLoginAtUtc,
                cancellationToken);

            await _audit.PublishAsync(
                action: lockoutEnabled ? "auth.account_locked" : "auth.login_failed",
                message: lockoutEnabled
                    ? $"User '{user.UserName}' locked out after {failed} failed attempts."
                    : $"Login failed for user '{user.UserName}': invalid password (attempt {failed}).",
                level: "Warning",
                userId: user.Id,
                userName: user.UserName,
                data: new Dictionary<string, object> { ["failedAttempts"] = failed },
                cancellationToken: cancellationToken);

            throw new UnauthorizedAccessException("Invalid username/email or password.");
        }

        user.AccessFailedCount = 0;
        user.LockoutEnabled = false;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTimeOffset.UtcNow;

        var pwdChanged = user.PasswordChangedAtUtc;
        var passwordExpiresAtUtc = pwdChanged.HasValue
            ? pwdChanged.Value.AddDays(_security.PasswordExpiryDays)
            : (DateTimeOffset?)null;

        var isExpired = passwordExpiresAtUtc.HasValue && passwordExpiresAtUtc.Value <= DateTimeOffset.UtcNow;
        if (!pwdChanged.HasValue || isExpired)
            user.MustChangePassword = true;

        await _userRepository.UpdateSecurityStateAsync(
            user.Id,
            accessFailedCount: user.AccessFailedCount,
            lockoutEnabled: user.LockoutEnabled,
            lockoutEndUtc: user.LockoutEndUtc,
            mustChangePassword: user.MustChangePassword,
            passwordChangedAtUtc: user.PasswordChangedAtUtc,
            lastLoginAtUtc: user.LastLoginAtUtc,
            cancellationToken);

        await _audit.PublishAsync(
            action: "auth.login_success",
            message: $"User '{user.UserName}' logged in successfully.",
            userId: user.Id,
            userName: user.UserName,
            cancellationToken: cancellationToken);

        var response = await GenerateTokensAsync(user, request.RememberMe, cancellationToken);
        response.MustChangePassword = user.MustChangePassword;
        response.PasswordExpiresAtUtc = passwordExpiresAtUtc;
        return response;
    }

    /// <summary>
    /// P3.5: Verifies the supplied password against the user's stored hash without
    /// issuing tokens. Used as a re-authentication gate for critical actions
    /// (delete user, etc.) so a hijacked session alone cannot complete them.
    /// </summary>
    public async Task<bool> VerifyPasswordAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
            return false;

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) return false;

        return _passwordHasher.VerifyHashedPassword(user.PasswordHash, password);
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        // P3.2: throw on invalid password policy.
        await _changePasswordValidator.ValidateAndThrowAsync(
            new ChangePasswordRequestDto { CurrentPassword = currentPassword, NewPassword = newPassword },
            cancellationToken);
        
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or inactive.");

        if (!_passwordHasher.VerifyHashedPassword(user.PasswordHash, currentPassword))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        if (string.Equals(newPassword, user.UserName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("New password must not match the username.");

        var newHash = _passwordHasher.HashPassword(newPassword);
        await _userRepository.UpdatePasswordAsync(userId, newHash, DateTimeOffset.UtcNow, cancellationToken);
        await _userRepository.IncrementAuthzVersionAsync(userId, cancellationToken);

        await _audit.PublishAsync(
            action: "auth.password_changed",
            message: $"User '{user.UserName}' changed password.",
            userId: user.Id,
            userName: user.UserName,
            cancellationToken: cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        // P3.2: throw on invalid refresh token request.
        await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);
        
        var existing = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (existing is null || existing.IsRevoked || existing.IsExpired)
            throw new InvalidOperationException("Invalid or expired refresh token.");

        var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new InvalidOperationException("User not found or inactive.");

        if (_security.SingleClientLoginEnabled)
        {
            if (!existing.SessionId.HasValue ||
                !user.ActiveSessionId.HasValue ||
                existing.SessionId.Value != user.ActiveSessionId.Value)
            {
                throw new UnauthorizedAccessException("This session has been signed out because the account logged in on another client.");
            }
        }

        var pwdChanged = user.PasswordChangedAtUtc;
        var passwordExpiresAtUtc = pwdChanged.HasValue
            ? pwdChanged.Value.AddDays(_security.PasswordExpiryDays)
            : (DateTimeOffset?)null;
        var isExpired = passwordExpiresAtUtc.HasValue && passwordExpiresAtUtc.Value <= DateTimeOffset.UtcNow;
        if (!pwdChanged.HasValue || isExpired)
            user.MustChangePassword = true;

        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(existing, cancellationToken);

        var originalLifetimeDays = (existing.ExpiresAtUtc - existing.CreatedAtUtc).TotalDays;
        var rememberMe = originalLifetimeDays > _jwtSettings.RefreshTokenDays + 0.5;

        if (isExpired || !pwdChanged.HasValue)
        {
            await _userRepository.UpdateSecurityStateAsync(
                user.Id,
                user.AccessFailedCount,
                user.LockoutEnabled,
                user.LockoutEndUtc,
                user.MustChangePassword,
                user.PasswordChangedAtUtc,
                user.LastLoginAtUtc,
                cancellationToken);
        }

        var response = await GenerateTokensAsync(user, rememberMe, cancellationToken, keepCurrentSession: true);
        response.MustChangePassword = user.MustChangePassword;
        response.PasswordExpiresAtUtc = passwordExpiresAtUtc;
        return response;
    }

    public async Task RevokeRefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
        => await RevokeRefreshTokenAsync(request, revokeAll: false, cancellationToken);

    /// <summary>
    /// P3.3: Logout. When <paramref name="revokeAll"/> is true, every refresh token
    /// for the owning user is revoked and the user's authz_version is bumped so any
    /// other active access token loses its permission claims on next request.
    /// </summary>
    public async Task RevokeRefreshTokenAsync(RefreshTokenRequestDto request, bool revokeAll, CancellationToken cancellationToken = default)
    {
        var existing = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (existing is null || existing.IsRevoked || existing.IsExpired)
            throw new InvalidOperationException("Invalid or expired refresh token.");

        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(existing, cancellationToken);

        if (revokeAll && !string.IsNullOrWhiteSpace(existing.UserId))
        {
            // P3.3: Revoke ALL refresh tokens for this user (every device/browser).
            await _refreshTokenRepository.RevokeByUserIdAsync(existing.UserId, DateTimeOffset.UtcNow, null, cancellationToken);
            // P3.3: Bumping authz_version forces existing access tokens to fail the
            // permission snapshot check in JwtBearerEvents (AuthExtensions.cs).
            await _userRepository.IncrementAuthzVersionAsync(existing.UserId, cancellationToken);
            await _userRepository.UpdateActiveSessionAsync(existing.UserId, null, null, cancellationToken);

            await _audit.PublishAsync(
                action: "auth.logout_all",
                message: $"User '{existing.UserId}' logged out from all sessions.",
                userId: existing.UserId,
                cancellationToken: cancellationToken);
            return;
        }

        await _audit.PublishAsync(
            action: "auth.logout",
            message: $"User '{existing.UserId}' logged out.",
            userId: existing.UserId,
            cancellationToken: cancellationToken);

        if (_security.SingleClientLoginEnabled &&
            !string.IsNullOrWhiteSpace(existing.UserId) &&
            existing.SessionId.HasValue)
        {
            var activeSessionId = await _userRepository.GetActiveSessionIdAsync(existing.UserId, cancellationToken);
            if (activeSessionId.HasValue && activeSessionId.Value == existing.SessionId.Value)
            {
                await _userRepository.UpdateActiveSessionAsync(existing.UserId, null, null, cancellationToken);
            }
        }
    }

    public async Task<User> RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        // P3.2: throw on invalid register payload (enforces password policy).
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        
        var existingByUser = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);
        if (existingByUser is not null)
            throw new InvalidOperationException("Username already exists.");

        var existingByEmail = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingByEmail is not null)
            throw new InvalidOperationException("Email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Roles = new List<string>(),
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAtUtc = DateTimeOffset.UtcNow,
            AuthzVersion = 1
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        return user;
    }

    public async Task<CurrentUserAuthzDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null) return null;

        var permissions = await ResolvePermissionsAsync(user, cancellationToken);

        return new CurrentUserAuthzDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            IsActive = user.IsActive,
            DisplayDefault = user.DisplayDefault,
            MeasurementType = user.MeasurementType,
            MeasurementDisplayMode = user.MeasurementDisplayMode,
            PhoneNumber = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            MustChangePassword = user.MustChangePassword,
            AuthzVersion = user.AuthzVersion,
            Roles = user.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RoleIds = user.RoleIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Departments = user.Departments.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Permissions = permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private Task<AuthResponseDto> GenerateTokensAsync(User user, CancellationToken cancellationToken)
        => GenerateTokensAsync(user, rememberMe: false, cancellationToken);

    private async Task<AuthResponseDto> GenerateTokensAsync(
        User user,
        bool rememberMe,
        CancellationToken cancellationToken,
        bool keepCurrentSession = false)
    {
        var accessExpires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);
        var refreshDays = rememberMe
            ? _jwtSettings.RefreshTokenRememberMeDays
            : _jwtSettings.RefreshTokenDays;
        var refreshExpires = DateTimeOffset.UtcNow.AddDays(refreshDays);

        if (_security.SingleClientLoginEnabled)
        {
            if (!keepCurrentSession || !user.ActiveSessionId.HasValue)
            {
                user.ActiveSessionId = Guid.NewGuid();
                await _userRepository.UpdateActiveSessionAsync(user.Id, user.ActiveSessionId, user.LastLoginAtUtc, cancellationToken);
                await _refreshTokenRepository.RevokeByUserIdAsync(user.Id, DateTimeOffset.UtcNow, user.ActiveSessionId, cancellationToken);
            }
        }
        else
        {
            user.ActiveSessionId = null;
        }

        var permissions = await ResolvePermissionsAsync(user, cancellationToken);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, permissions, accessExpires, user.AuthzVersion);

        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = refreshExpires,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SessionId = user.ActiveSessionId
        };

        await _refreshTokenRepository.CreateAsync(refresh, cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessExpires,
            RefreshToken = refresh.Token,
            RefreshTokenExpiresAtUtc = refreshExpires
        };
    }

    private async Task<HashSet<string>> ResolvePermissionsAsync(User user, CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (user.Roles is null)
            return permissions;

        foreach (var roleName in user.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
            if (role?.Permissions is null) continue;

            foreach (var perm in role.Permissions)
                permissions.Add(perm);
        }

        return permissions;
    }
}
