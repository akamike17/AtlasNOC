using Microsoft.AspNetCore.Http;

namespace AtlasNOC.Services.Ui;

/// <summary>
/// Shared configuration for the authenticated NOC UI (cookie-based session).
/// The cookie bridges a validated API key into a HTTP-only session cookie so the
/// operator browser never holds the raw key. API endpoints keep using X-API-Key.
/// </summary>
public static class AtlasNocUiAuth
{
    public const string UiScheme = "AtlasNocUi";
    public const string CookieName = "AtlasNOC.Ui";
    public const string LoginPath = "/account/login";
    public const string LogoutPath = "/account/logout";
    public const string AccessDeniedPath = "/account/access-denied";

    /// <summary>
    /// True for non-secret, anonymous-server assets and (Development only) Swagger.
    /// These are public browser resources; they must load before an operator logs in.
    /// They carry no secrets, credentials, connection strings or stack traces.
    /// </summary>
    public static bool IsPublicAssetPath(PathString path, bool isDevelopment)
    {
        var value = path.Value ?? string.Empty;

        // Server-side static assets shipped by the project (CSS/JS/fonts/images).
        if (value.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            return true;

        // Swagger (Development) — documented API surface for local testing only.
        if (isDevelopment && value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}