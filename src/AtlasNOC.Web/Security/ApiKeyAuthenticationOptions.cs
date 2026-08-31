using Microsoft.AspNetCore.Authentication;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Opciones del esquema de autenticación por API key. La cabecera aceptada es
/// exclusivamente <c>X-Api-Key</c> (especificación §2).
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}