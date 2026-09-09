using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Requisito de rol humano para endpoints API que necesitan autorización por roles
/// (no por scopes de API key). Se usa para mutaciones POST/PUT/DELETE en /api/*
/// cuando el actor es un usuario con cookie.
/// </summary>
public sealed class HumanRoleRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> AllowedRoles { get; }

    public HumanRoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles ?? Array.Empty<string>();
    }
}

/// <summary>
/// Evalúa <see cref="HumanRoleRequirement"/>. Solo se satisface si el principal
/// es un usuario humano autenticado por cookie y tiene uno de los roles permitidos.
/// NO satisface principals de API key (auth_type=api_key).
/// </summary>
public class HumanRoleAuthorizationHandler : AuthorizationHandler<HumanRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, HumanRoleRequirement requirement)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // No permitir que API keys usen roles humanos
        if (IsApiKeyPrincipal(user))
        {
            return Task.CompletedTask;
        }

        // Usuario humano: verificar rol
        var userRoles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
        if (requirement.AllowedRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    internal static bool IsApiKeyPrincipal(System.Security.Claims.ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true
           && user.FindFirst("auth_type")?.Value == "api_key";
}

/// <summary>
/// Extensiones para registrar políticas de rol humano.
/// </summary>
public static class HumanRolePolicyExtensions
{
    public static AuthorizationOptions AddHumanRolePolicies(this AuthorizationOptions options)
    {
        // Roles humanos definidos en ApplicationRole
        // Administrator: todo
        // NocOperator: operaciones de NOC (no admin de seguridad)
        // ReadOnly: solo lectura

        options.AddPolicy("Human.Administrator", policy => policy
            .AddRequirements(new HumanRoleRequirement("Administrator")));

        options.AddPolicy("Human.NocOperatorOrAdmin", policy => policy
            .AddRequirements(new HumanRoleRequirement("NocOperator", "Administrator")));

        options.AddPolicy("Human.ReadOnlyOrOperatorOrAdmin", policy => policy
            .AddRequirements(new HumanRoleRequirement("ReadOnly", "NocOperator", "Administrator")));

        return options;
    }
}