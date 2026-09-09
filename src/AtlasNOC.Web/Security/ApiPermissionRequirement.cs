using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Requisito de permiso unificado para endpoints API: satisface (API key con scope requerido) O (humano con rol permitido).
/// Elimina la duplicación de ApiScopeRequirement + HumanRoleRequirement + RequireAssertion manual.
/// </summary>
public sealed class ApiPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Scope requerido para API keys (ej. "topology.read", "sites.write").</summary>
    public string Scope { get; }

    /// <summary>Roles humanos permitidos (ej. "NocOperator", "Administrator"). Si vacío, solo API keys.</summary>
    public IReadOnlyCollection<string> HumanRoles { get; }

    /// <summary>
    /// Crea un requisito de permiso API.
    /// </summary>
    /// <param name="scope">Scope requerido para API keys.</param>
    /// <param name="humanRoles">Roles humanos permitidos. Si vacío/null, solo API keys pueden acceder.</param>
    public ApiPermissionRequirement(string scope, params string[] humanRoles)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        HumanRoles = humanRoles?.Length > 0
            ? humanRoles.Where(r => !string.IsNullOrWhiteSpace(r)).ToList().AsReadOnly()
            : Array.Empty<string>();
    }
}

/// <summary>
/// Evalúa <see cref="ApiPermissionRequirement"/> con semántica OR:
/// - API key válida con scope requerido → éxito
/// - Usuario humano con cookie válida y rol permitido → éxito
/// - Cualquier otro caso → fallo
/// </summary>
public class ApiPermissionAuthorizationHandler : AuthorizationHandler<ApiPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ApiPermissionRequirement requirement)
    {
        var user = context.User;

        // No autenticado -> no cumple.
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // 1. API key: exige el scope concreto (o comodín *).
        if (IsApiKeyPrincipal(user))
        {
            var scopes = user.FindAll("scope").Select(c => c.Value);
            if (scopes.Any(s => s == requirement.Scope || s == "*" || s == "*.*"))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }

        // 2. Humano (cookie): exige uno de los roles permitidos.
        if (requirement.HumanRoles.Count > 0)
        {
            var userRoles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
            if (requirement.HumanRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }

    internal static bool IsApiKeyPrincipal(System.Security.Claims.ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true
           && user.FindFirst("auth_type")?.Value == "api_key";
}

/// <summary>
/// Extensiones para registrar políticas de permiso API unificadas.
/// </summary>
public static class ApiPermissionPolicyExtensions
{
    /// <summary>
    /// Registra las políticas de permiso API: una por scope, combinando scope + roles humanos.
    /// </summary>
    public static AuthorizationOptions AddApiPermissionPolicies(this AuthorizationOptions options)
    {
        // Políticas de lectura operacional (ReadOnly, NocOperator, Administrator)
        options.AddPolicy("Api.TopologyRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("topology.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.MetricsRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("metrics.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.DevicesRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("devices.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.SitesRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("sites.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.SubscribersRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("subscribers.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.AlertsRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("alerts.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.IncidentsRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("incidents.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.IntegrationsRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("integrations.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.SystemRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("system.read", "ReadOnly", "NocOperator", "Administrator")));

        options.AddPolicy("Api.DiscoveryRead", policy => policy
            .AddRequirements(new ApiPermissionRequirement("discovery.run", "ReadOnly", "NocOperator", "Administrator")));

        // Políticas de escritura NOC (NocOperator, Administrator)
        options.AddPolicy("Api.SitesWrite", policy => policy
            .AddRequirements(new ApiPermissionRequirement("sites.write", "NocOperator", "Administrator")));

        options.AddPolicy("Api.SubscribersWrite", policy => policy
            .AddRequirements(new ApiPermissionRequirement("subscribers.write", "NocOperator", "Administrator")));

        options.AddPolicy("Api.AlertsWrite", policy => policy
            .AddRequirements(new ApiPermissionRequirement("alerts.write", "NocOperator", "Administrator")));

        options.AddPolicy("Api.IncidentsWrite", policy => policy
            .AddRequirements(new ApiPermissionRequirement("incidents.write", "NocOperator", "Administrator")));

        options.AddPolicy("Api.DiscoveryRun", policy => policy
            .AddRequirements(new ApiPermissionRequirement("discovery.run", "NocOperator", "Administrator")));

        // Política de solo administrador
        options.AddPolicy("Api.AdminOnly", policy => policy
            .AddRequirements(new ApiPermissionRequirement("admin.only", "Administrator")));

        // Política combinada para endpoints API: acepta autenticado (cookie o API key)
        // cada controlador añade su política específica
        options.AddPolicy("ApiAuthenticated", policy => policy
            .RequireAuthenticatedUser());

        return options;
    }
}