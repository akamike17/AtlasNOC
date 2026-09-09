using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de integraciones: expone las credenciales y API keys administradas (sin secretos).</summary>
[ApiController]
[Route("api/integrations")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class IntegrationsApiController : ControllerBase
{
    private readonly IApiKeyService _apiKeys;
    private readonly ICredentialService _credentials;

    public IntegrationsApiController(IApiKeyService apiKeys, ICredentialService credentials)
    {
        _apiKeys = apiKeys;
        _credentials = credentials;
    }

    [HttpGet("api-keys")]
    [Authorize(Policy = "Api.IntegrationsRead")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyLiteDto>>> ListApiKeys(CancellationToken ct)
        => Ok(await _apiKeys.ListApiKeysAsync(ct));

    [HttpGet("credentials")]
    [Authorize(Policy = "Api.IntegrationsRead")]
    public async Task<ActionResult<IReadOnlyList<CredentialDto>>> ListCredentials(CancellationToken ct)
        => Ok(await _credentials.ListCredentialsAsync(ct));
}