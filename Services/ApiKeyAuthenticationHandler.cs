using System.Security.Claims;
using System.Text.Encodings.Web;
using AtlasNOC.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Services;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AtlasApiKey";
    private const string HeaderName = "X-API-Key";
    public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
            return AuthenticateResult.NoResult();

        var keyStore = Context.RequestServices.GetRequiredService<ApiKeyStore>();
        var key = await keyStore.ValidateAsync(values[0]!, Context.RequestAborted);
        if (key is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, key.Id.ToString()),
            new Claim(ClaimTypes.Name, key.Owner),
            new Claim(ClaimTypes.Role, key.Role)
        }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), SchemeName));
    }
}
