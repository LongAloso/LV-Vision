using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IdentityService.Api.Security;

public sealed class InternalApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "InternalApiKey";
    private const string HeaderName = "X-Internal-Api-Key";
    private readonly IConfiguration _configuration;

    public InternalApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedKey = values.FirstOrDefault();
        var expectedKey = _configuration["InternalApi:Key"]
            ?? _configuration["Services:ConfigService:InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(providedKey) ||
            string.IsNullOrWhiteSpace(expectedKey) ||
            !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal API key."));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "internal-service"),
            new Claim("internal_service", "true")
        ], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
