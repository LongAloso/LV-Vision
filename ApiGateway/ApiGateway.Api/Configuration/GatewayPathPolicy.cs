namespace ApiGateway.Api.Configuration;

public static class GatewayPathPolicy
{
    public static bool IsInternalConfigPath(PathString path) =>
        path.StartsWithSegments("/identityservice/config", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/identityservice/api/config", StringComparison.OrdinalIgnoreCase);
}
