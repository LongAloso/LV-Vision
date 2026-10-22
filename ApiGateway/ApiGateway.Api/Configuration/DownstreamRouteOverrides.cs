using Microsoft.Extensions.Configuration;

namespace ApiGateway.Api.Configuration;

internal static class DownstreamRouteOverrides
{
    public static int Apply(ConfigurationManager configuration)
    {
        var overrides = new Dictionary<string, string?>();
        foreach (var route in configuration.GetSection("Routes").GetChildren())
        {
            var routeName = route["RouteName"];
            if (string.IsNullOrWhiteSpace(routeName))
                continue;

            var optionsPath = $"DownstreamServices:{routeName}";
            var section = configuration.GetSection(optionsPath);
            var host = section["Host"]?.Trim();
            var portValue = section["Port"]?.Trim();

            if (string.IsNullOrWhiteSpace(host) && string.IsNullOrWhiteSpace(portValue))
                continue;

            if (!string.IsNullOrWhiteSpace(portValue) &&
                (!int.TryParse(portValue, out var parsedPort) || parsedPort is < 1 or > 65535))
                throw new InvalidOperationException($"{optionsPath}:Port must be between 1 and 65535.");

            if (!string.IsNullOrWhiteSpace(host))
                overrides[$"Routes:{route.Key}:DownstreamHostAndPorts:0:Host"] = host;

            if (!string.IsNullOrWhiteSpace(portValue))
                overrides[$"Routes:{route.Key}:DownstreamHostAndPorts:0:Port"] = portValue;
        }

        if (overrides.Count > 0)
            configuration.AddInMemoryCollection(overrides);
        return overrides.Count;
    }
}
