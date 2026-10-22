using FluentAssertions;
using FluentValidation;
using IdentityService.Application.Configuration;
using IdentityService.Application.DTOs.Auth;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IdentityService.Tests;

public class AuthServiceTests
{
    private AuthService BuildService(
        out Mock<IUserRepository> userRepoMock,
        out Mock<IRefreshTokenRepository> refreshRepoMock,
        out Mock<IPasswordHasher> hasherMock,
        out Mock<IJwtTokenGenerator> jwtMock,
        out Mock<IRoleRepository> roleRepoMock, 
        out Mock<IValidator<LoginRequestDto>> loginValidatorMock,
        out Mock<IValidator<RefreshTokenRequestDto>> refreshTokenValidatorMock,
        out Mock<IValidator<RegisterUserRequestDto>> registerValidatorMock,
        out Mock<IValidator<ChangePasswordRequestDto>> changePasswordValidatorMock,
        out Mock<IAuditEventPublisher> auditMock
        )
    {
        userRepoMock = new Mock<IUserRepository>();
        refreshRepoMock = new Mock<IRefreshTokenRepository>();
        hasherMock = new Mock<IPasswordHasher>();
        jwtMock = new Mock<IJwtTokenGenerator>();
        roleRepoMock = new Mock<IRoleRepository>();
        loginValidatorMock = new Mock<IValidator<LoginRequestDto>>();
        refreshTokenValidatorMock = new Mock<IValidator<RefreshTokenRequestDto>>();
        registerValidatorMock = new Mock<IValidator<RegisterUserRequestDto>>();
        changePasswordValidatorMock = new Mock<IValidator<ChangePasswordRequestDto>>();
        auditMock = new Mock<IAuditEventPublisher>();

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = "test-key-1234567890-ABCDEFG!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });

        var securitySettings = Options.Create(new SecuritySettings
        {
            PasswordMinLength = 8,
            PasswordExpiryDays = 90,
            MaxFailedAccessAttempts = 10,
            LockoutMinutes = 15
        });

        return new AuthService(
            userRepoMock.Object,
            refreshRepoMock.Object,
            hasherMock.Object,
            jwtMock.Object,
            roleRepoMock.Object,
            jwtSettings,
            securitySettings,
            loginValidatorMock.Object,
            refreshTokenValidatorMock.Object,
            registerValidatorMock.Object,
            changePasswordValidatorMock.Object,
            auditMock.Object
            );
    }

    [Fact]
    public async Task Login_Should_Throw_When_User_Not_Found()
    {
        var service = BuildService(out var userRepo, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        userRepo.Setup(r => r.GetByUserNameOrEmailAsync("id1", default))
            .ReturnsAsync((User?)null);

        var req = new LoginRequestDto { UserNameOrEmail = "id1", Password = "p1" };

        var act = () => service.LoginAsync(req);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid username/email or password.");
    }

    [Fact]
    public async Task Login_Should_Throw_When_Password_Invalid()
    {
        var service = BuildService(out var userRepo, out _, out var hasher, out _, out _, out _, out _, out _, out _, out _);

        var user = new User
        {
            Id = "U1",
            UserName = "u1",
            Email = "u1@test",
            PasswordHash = "HASH",
            IsActive = true
        };

        userRepo.Setup(r => r.GetByUserNameOrEmailAsync("id1", default))
            .ReturnsAsync(user);

        hasher.Setup(h => h.VerifyHashedPassword("HASH", "p1")).Returns(false);

        var req = new LoginRequestDto { UserNameOrEmail = "id1", Password = "p1" };

        var act = () => service.LoginAsync(req);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid username/email or password.");
    }

    [Fact]
    public async Task Login_Should_Return_Tokens_When_Valid()
    {
        var service = BuildService(out var userRepo, out var refreshRepo, out var hasher, out var jwt, out var roleRepo, 
            out var loginValidator, out var refreshTokenValidator, out var registerValidator, out var changePasswordValidator, out _);

        var user = new User
        {
            Id = "U1",
            UserName = "u1",
            Email = "u1@test",
            PasswordHash = "HASH",
            IsActive = true,
            Roles = new List<string> { "Admin" }
        };

        userRepo.Setup(r => r.GetByUserNameOrEmailAsync("id1", default))
            .ReturnsAsync(user);

        hasher.Setup(h => h.VerifyHashedPassword("HASH", "p1")).Returns(true);

        roleRepo.Setup(r => r.GetByNameAsync("Admin", default))
            .ReturnsAsync(new Role
            {
                Id = "R1",
                Name = "Admin",
                Permissions = new List<string> { "Users.Read" }
            });

        jwt.Setup(j => j.GenerateAccessToken(
                user,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<long?>()))
            .Returns("ACCESS_TOKEN");

        var req = new LoginRequestDto { UserNameOrEmail = "id1", Password = "p1" };

        var result = await service.LoginAsync(req);

        result.AccessToken.Should().Be("ACCESS_TOKEN");
        result.RefreshToken.Should().NotBeNullOrEmpty();
        refreshRepo.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>(), default), Times.Once);
    }
}
