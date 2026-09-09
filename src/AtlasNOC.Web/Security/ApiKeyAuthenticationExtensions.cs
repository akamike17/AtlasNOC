using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Extensión que registra la autenticación por API key y las políticas de scope.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Registra el esquema de API key (sólo lectura de <c>X-Api-Key</c>) como
    /// esquema adicional. La cookie sigue siendo el esquema por defecto para MVC.
    /// </summary>
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.Scheme, _ => { });
    }

    /// <summary>
    /// Registra las políticas de scope: una por scope declarado en <see cref="ApiScopes.All"/>.
    /// Además registra la política por defecto para <c>/api/*</c>: exigir usuario
    /// autenticado por cookie humana o por API key válida.
    /// </summary>
    public static AuthorizationOptions AddApiScopePolicies(this AuthorizationOptions options)
    {
        foreach (var scope in ApiScopes.All)
        {
            options.AddPolicy(scope, policy => policy
                .AddRequirements(new ApiScopeRequirement(scope)));
        }

        // Políticas combinadas para Discovery: scope O rol humano
        options.AddPolicy("DiscoveryRunPolicy", policy => policy
            .RequireAssertion(ctx =>
            {
                var user = ctx.User;
                if (user.Identity?.IsAuthenticated != true) return false;
                // API key con scope discovery.run
                if (user.FindFirst("auth_type")?.Value == "api_key")
                {
                    var scopes = user.FindAll("scope").Select(c => c.Value);
                    return scopes.Any(s => s == "discovery.run" || s == "*" || s == "*.*");
                }
                // Humano con rol NocOperator o Administrator
                var roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
                return roles.Any(r => r == "NocOperator" || r == "Administrator");
            }));

        options.AddPolicy("DiscoveryReadPolicy", policy => policy
            .RequireAssertion(ctx =>
            {
                var user = ctx.User;
                if (user.Identity?.IsAuthenticated != true) return false;
                // API key con scope discovery.run
                if (user.FindFirst("auth_type")?.Value == "api_key")
                {
                    var scopes = user.FindAll("scope").Select(c => c.Value);
                    return scopes.Any(s => s == "discovery.run" || s == "*" || s == "*.*");
                }
                // Humano con rol ReadOnly, NocOperator o Administrator
                var roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
                return roles.Any(r => r == "ReadOnly" || r == "NocOperator" || r == "Administrator");
            }));

        // Política combinada para endpoints API: acepta cookie humana (para UI que
        // consume la API) o API key. Cada controlador API añade además su scope.
        options.AddPolicy("ApiAuthenticated", policy => policy
            .RequireAuthenticatedUser());

        return options;
    }
}