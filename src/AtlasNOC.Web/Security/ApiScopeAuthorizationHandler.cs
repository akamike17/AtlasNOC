using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Evalúa <see cref="ApiScopeRequirement"/>. Sólo se satisface si el principal fue
/// autenticado por API key (<c>auth_type=api_key</c>) y posee el claim <c>scope</c>
/// requerido. Un principal humano (cookie) NO satisface un requisito de scope, lo
/// que evita otorgar permisos de API a sesiones de navegador.
/// </summary>
public class ApiScopeAuthorizationHandler : AuthorizationHandler<ApiScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ApiScopeRequirement requirement)
    {
        var user = context.User;

        // No autenticado -> no cumple.
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // 1. API key: exige el scope concreto (o comodín).
        if (IsApiKeyPrincipal(user))
        {
            var scopes = user.FindAll("scope").Select(c => c.Value);
            if (scopes.Any(s => s == requirement.Scope || s == "*" || s == "*.*"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

        // 2. Humano (cookie): NO satisface scope. Las vistas MVC usan roles.
        // Las APIs mutables deben usar políticas de rol separadas, no scopes.
        return Task.CompletedTask;
    }

    internal static bool IsApiKeyPrincipal(System.Security.Claims.ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true
           && user.FindFirst("auth_type")?.Value == "api_key";
}