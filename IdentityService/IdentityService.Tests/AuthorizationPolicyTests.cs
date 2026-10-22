using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace IdentityService.Tests;

// Requirement: cần 1 permission cụ thể
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

// Handler: kiểm tra claim "permission"
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class AuthorizationPolicyTests
{
    private static AuthorizationPolicy BuildUsersReadPolicy()
    {
        var requirement = new PermissionRequirement("Users.Read");

        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(requirement)
            .Build();

        return policy;
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, AuthorizationPolicy policy)
    {
        var context = new AuthorizationHandlerContext(policy.Requirements, user, resource: null);
        var handler = new PermissionHandler();

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task User_With_UsersRead_Permission_Should_Succeed()
    {
        // Arrange
        var policy = BuildUsersReadPolicy();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("permission", "Users.Read")
        }, "Test");

        var user = new ClaimsPrincipal(identity);

        // Act
        var succeeded = await AuthorizeAsync(user, policy);

        // Assert
        succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task User_Without_UsersRead_Permission_Should_Fail()
    {
        // Arrange
        var policy = BuildUsersReadPolicy();

        var identity = new ClaimsIdentity(); // không có claim "permission"
        var user = new ClaimsPrincipal(identity);

        // Act
        var succeeded = await AuthorizeAsync(user, policy);

        // Assert
        succeeded.Should().BeFalse();
    }
}
