using System.Security.Claims;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Extrae la identidad del actor a partir de <see cref="ClaimsPrincipal"/>, tanto para
/// cookie humana como para API key. Devuelve valores seguros (nunca secretos) para auditoría.
/// </summary>
public static class ApiActor
{
    public static string Id(ClaimsPrincipal? user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "api-key";

    public static string Name(ClaimsPrincipal? user)
        => user?.FindFirstValue(ClaimTypes.Name) ?? user?.FindFirstValue("sub") ?? "api-key";

    public static string Role(ClaimsPrincipal? user)
        => user?.FindFirstValue(ClaimTypes.Role) ?? "api-key";
}