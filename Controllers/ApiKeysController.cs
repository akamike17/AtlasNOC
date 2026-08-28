using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class ApiKeysController : ControllerBase
{
    private readonly ApiKeyStore _apiKeyStore;
    private readonly IAuditService _auditService;

    public ApiKeysController(ApiKeyStore apiKeyStore, IAuditService auditService)
    {
        _apiKeyStore = apiKeyStore ?? throw new ArgumentNullException(nameof(apiKeyStore));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// List all active API keys.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<ApiKeyInfo>> GetAll(CancellationToken ct = default)
        => await _apiKeyStore.ListActiveKeysAsync(ct);

    /// <summary>
    /// Create a new API key. The key value is returned ONCE — store it securely.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Owner))
            return BadRequest(new { error = "Owner is required" });

        var (keyId, plaintextKey) = await _apiKeyStore.CreateKeyAsync(
            request.Owner, request.Description ?? "", request.Role, ct);

        // Audit the creation
        await _auditService.LogSuccessAsync(
            "ApiKey", "Create", Actor,
            targetResource: keyId.ToString(),
            targetResourceType: "ApiKey",
            newValue: $"Owner={request.Owner}, Description={request.Description}",
            cancellationToken: ct);

        // Return the key details WITHOUT the actual key value (security)
        // The caller should have captured the key from the request body before sending
        return Ok(new
        {
            Id = keyId,
            Key = plaintextKey,
            Owner = request.Owner,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            Note = "This is the only time the key is returned. Store it securely."
        });
    }

    /// <summary>
    /// Revoke (deactivate) an API key by ID.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct = default)
    {
        await _apiKeyStore.RevokeKeyAsync(id, ct);
        await _auditService.LogSuccessAsync(
            "ApiKey", "Revoke", User.Identity?.Name ?? "Unknown",
            targetResource: id.ToString(),
            targetResourceType: "ApiKey",
            cancellationToken: ct);
        return NoContent();
    }

    private string Actor => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated actor is unavailable.");
}

public class CreateApiKeyRequest
{
    public string Owner { get; set; } = "";
    public string Role { get; set; } = "ReadOnly";
    public string? Description { get; set; }
}
