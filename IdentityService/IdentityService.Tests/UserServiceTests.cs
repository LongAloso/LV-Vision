using FluentAssertions;
using FluentValidation;
using IdentityService.Application.DTOs.Users;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Moq;
using Xunit;

namespace IdentityService.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task Create_Should_Hash_Password_And_Save_User()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var repodept = new Mock<IDepartmentRepository>();
        var repouserdept = new Mock<IUserDepartmentRepository>();
        var creatUserValidator = new Mock<IValidator<CreateUserRequestDto>>();
        var updateUserValidator = new Mock<IValidator<UpdateUserRequestDto>>();

        hasher.Setup(h => h.HashPassword("pwd")).Returns("HASH");

        var service = new UserService(repo.Object, hasher.Object, repodept.Object,repouserdept.Object, creatUserValidator.Object, updateUserValidator.Object);

        var dto = new CreateUserRequestDto
        {
            UserName = "u1",
            Email = "u1@test",
            Password = "pwd",
            Roles = new List<string> { "Admin" }
        };

        var result = await service.CreateAsync(dto);

        result.UserName.Should().Be("u1");
        result.Roles.Should().Contain("Admin");

        repo.Verify(r => r.CreateAsync(It.Is<User>(u => u.PasswordHash == "HASH"), default), Times.Once);
    }

    [Fact]
    public async Task Delete_Should_Return_False_When_User_Not_Found()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var repodept = new Mock<IDepartmentRepository>();
        var repouserdept = new Mock<IUserDepartmentRepository>();
        var creatUserValidator = new Mock<IValidator<CreateUserRequestDto>>();
        var updateUserValidator = new Mock<IValidator<UpdateUserRequestDto>>();

        repo.Setup(r => r.GetByIdAsync("ID1", default)).ReturnsAsync((User?)null);

        var service = new UserService(repo.Object, hasher.Object, repodept.Object, repouserdept.Object, creatUserValidator.Object, updateUserValidator.Object);

        var ok = await service.DeleteAsync("ID1");

        ok.Should().BeFalse();
        repo.Verify(r => r.DeleteAsync(It.IsAny<string>(), default), Times.Never);
    }
}
