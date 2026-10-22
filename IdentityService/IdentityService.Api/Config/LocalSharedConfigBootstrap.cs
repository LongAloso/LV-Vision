namespace IdentityService.Api.Config;

public static class LocalSharedConfigBootstrap
{
    public static void Apply(WebApplicationBuilder builder)
    {
        const string sectionPath = "Services:IdentityService";

        var section = builder.Configuration.GetSection(sectionPath);
        if (!section.Exists())
        {
            throw new InvalidOperationException($"Missing configuration section {sectionPath}.");
        }

        var flat = section.AsEnumerable()
            .Where(x => x.Value is not null && !string.Equals(x.Key, sectionPath, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                x => x.Key.Substring(sectionPath.Length + 1),
                x => (string?)x.Value,
                StringComparer.OrdinalIgnoreCase);

        builder.Configuration.AddInMemoryCollection(flat);
        // Keep deployment secrets and endpoint overrides outside the tracked config registry.
        builder.Configuration.AddEnvironmentVariables();
    }
}
