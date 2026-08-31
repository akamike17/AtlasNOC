using System.Security.Claims;
using System.Text.Encodings.Web;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Web.Security;

/// <summary>
/// Autenticación por API key (especificación §2). Lee exclusivamente la cabecera
/// <c>X-Api-Key</c>:
///  1. rechaza cabecera ausente;
///  2. valida el prefijo (<c>atn_</c>);
///  3. calcula SHA-256 de la key completa;
///  4. busca por hash;
///  5. verifica: existe, <c>IsActive</c>, no expirada, no revocada;
///  6. genera un <see cref="ClaimsPrincipal"/> con <c>sub</c>, <c>auth_type</c>,
///     <c>api_key_id</c> y un claim <c>scope</c> por cada scope.
///
/// Nunca se guarda la key en claro. Una API key NO funciona como login humano:
/// el principal resultante usa <c>auth_type=api_key</c> y no posee claims de rol,
/// por lo que las vistas MVC protegidas por cookie/roles lo rechazan.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ExpectedPrefix = "atn_";

    private readonly IApiKeyRepository _apiKeys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeys)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[ApiKeyAuthenticationOptions.HeaderName];

        // 1. Cabecera ausente -> no autenticado (no falla el pipeline; permite
        //    caer a la autenticación por cookie para vistas MVC).
        if (string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        var key = header.ToString().Trim();

        // 2. Prefijo inválido -> rechazar.
        if (!key.StartsWith(ExpectedPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("API key malformada (prefijo inválido).");
        }

        // 3. SHA-256 de la key completa.
        var hash = ApiKeyService.HashKey(key);

        // 4. Búsqueda por hash.
        var apiKey = await _apiKeys.GetByHashAsync(hash, Context.RequestAborted);

        // 5. Verificación de validez.
        if (apiKey is null)
        {
            return AuthenticateResult.Fail("API key no reconocida.");
        }

        if (!apiKey.IsActive)
        {
            return AuthenticateResult.Fail("API key revocada o inactiva.");
        }

        if (apiKey.IsExpired)
        {
            return AuthenticateResult.Fail("API key expirada.");
        }

        // 6. Generar ClaimsPrincipal.
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new System.Security.Claims.Claim("sub", apiKey.OwnerUserId),
            new System.Security.Claims.Claim("auth_type", "api_key"),
            new System.Security.Claims.Claim("api_key_id", apiKey.Id.ToString()),
        };

        foreach (var scope in SplitScopes(apiKey.Scopes))
        {
            claims.Add(new System.Security.Claims.Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>Divide los scopes separados por espacio, coma, '|' o ';' y elimina vacíos.</summary>
    internal static IEnumerable<string> SplitScopes(string scopes)
    {
        return (scopes ?? string.Empty)
            .Split(new[] { ' ', ',', '|', ';', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal);
    }
}