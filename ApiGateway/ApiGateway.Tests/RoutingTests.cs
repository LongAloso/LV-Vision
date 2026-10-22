using System.Text.Json;
using ApiGateway.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ApiGateway.Tests;

public sealed class RoutingTests
{
    [Theory]
    [InlineData("/identityservice/config")]
    [InlineData("/identityservice/config/latest")]
    [InlineData("/identityservice/api/config/latest")]
    [InlineData("/IDENTITYSERVICE/API/CONFIG/latest")]
    public void InternalConfigPathsAreBlocked(string path)
        => Assert.True(GatewayPathPolicy.IsInternalConfigPath(new PathString(path)));

    [Theory]
    [InlineData("/identityservice/api/auth/login")]
    [InlineData("/identityservice/api/configuration")]
    public void NormalIdentityPathsAreNotBlocked(string path)
        => Assert.False(GatewayPathPolicy.IsInternalConfigPath(new PathString(path)));

    [Fact]
    public void IdentityRoutePreservesTheApiPrefixForLogin()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var routes = json.RootElement.GetProperty("Routes").EnumerateArray();
        var identity = routes.Single(route =>
            route.GetProperty("RouteName").GetString() == "identityservice");

        Assert.Equal("/identityservice/{everything}",
            identity.GetProperty("UpstreamPathTemplate").GetString());
        Assert.Equal("/{everything}",
            identity.GetProperty("DownstreamPathTemplate").GetString());
        Assert.Equal("identityservice",
            identity.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Host").GetString());
        Assert.Equal(5000,
            identity.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());
    }
}
