using System.Security.Claims;
using AtlasNOC.Infrastructure.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AtlasNOC.Tests.Unit;

/// <summary>
/// Tests de la autorización por scope (§2): un principal de API key sólo cumple
/// el requisito de scope si su auth_type es api_key y posee el scope requerido.
/// Un principal humano (cookie) NO cumple scope requirements; usa HumanRoleAuthorizationHandler.
/// </summary>
public class ApiScopeAuthorizationTests
{
    private static ClaimsPrincipal ApiKeyPrincipal(params string[] scopeGroups)
    {
        var claims = new List<Claim>
        {
            new("auth_type", "api_key"),
            new("sub", "user-1"),
        };
        foreach (var group in scopeGroups)
        {
            foreach (var scope in group.Split(new[] { ' ', ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim("scope", scope));
            }
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static ClaimsPrincipal HumanPrincipal() => new ClaimsPrincipal(new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Administrator"),
        }, "Identity.Application", ClaimTypes.Name, ClaimTypes.Role));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Theory]
    [InlineData("topology.read", "topology.read", true)]
    [InlineData("topology.read", "metrics.read", false)]
    [InlineData("*", "topology.read", true)]
    [InlineData("topology.read alerts.read", "alerts.read", true)]
    public async Task Api_key_with_scope_is_authorized(string keyScope, string requiredScope, bool expected)
    {
        var handler = new ApiScopeAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new[] { new ApiScopeRequirement(requiredScope) },
            ApiKeyPrincipal(keyScope), null);

        await handler.HandleAsync(ctx);

        Assert.Equal(expected, ctx.HasSucceeded);
    }

    [Fact]
    public async Task Api_key_without_matching_scope_fails()
    {
        var handler = new ApiScopeAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new[] { new ApiScopeRequirement(ApiScopes.TopologyRead) },
            ApiKeyPrincipal("metrics.read", "alerts.read"), null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Human_cookie_principal_does_not_satisfy_scope_uses_human_role_policy()
    {
        var handler = new ApiScopeAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new[] { new ApiScopeRequirement(ApiScopes.TopologyRead) },
            HumanPrincipal(), null);

        await handler.HandleAsync(ctx);

        // Human principals should NOT satisfy scope requirements
        // They use HumanRoleAuthorizationHandler instead
        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Anonymous_principal_never_satisfies_scope()
    {
        var handler = new ApiScopeAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new[] { new ApiScopeRequirement(ApiScopes.TopologyRead) },
            Anonymous(), null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public void Api_key_hash_is_deterministic_and_not_plaintext()
    {
        const string key = "atn_abcdef123456";
        var hash1 = ApiKeyService.HashKey(key);
        var hash2 = ApiKeyService.HashKey(key);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(key, hash1);
        Assert.Equal(64, hash1.Length); // SHA-256 hex
    }
}