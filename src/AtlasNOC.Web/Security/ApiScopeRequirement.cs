using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Requerimiento de autorización por scope: el principal debe poseer el claim
/// <c>scope</c> con el valor indicado (o el comodín <c>*</c>).
/// </summary>
public class ApiScopeRequirement : IAuthorizationRequirement
{
    public ApiScopeRequirement(string scope) => Scope = scope;

    public string Scope { get; }

    /// <summary>Crea una política de autorización que exige este scope.</summary>
    public static AuthorizationPolicy Policy(string scope)
        => new AuthorizationPolicyBuilder()
            .AddRequirements(new ApiScopeRequirement(scope))
            .Build();
}