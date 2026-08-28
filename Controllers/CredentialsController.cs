using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class CredentialsController : ControllerBase
{
    private readonly ICredentialService _service;
    private readonly ILogger<CredentialsController> _logger;

    public CredentialsController(ICredentialService service, ILogger<CredentialsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IReadOnlyList<Credential>> GetAll(CancellationToken ct = default)
        => await _service.GetAllAsync(ct);

    [HttpGet("active")]
    public async Task<IReadOnlyList<Credential>> GetActive(CancellationToken ct = default)
        => await _service.GetActiveAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var credential = await _service.GetByIdAsync(CredentialId.From(id), ct);
        return credential is null ? NotFound() : Ok(credential);
    }

    [HttpPost("v2c")]
    public async Task<IActionResult> CreateV2c(
        [FromBody] CreateV2cRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var credential = await _service.CreateV2cAsync(
                request.Name, request.Community, Actor,
                request.ExpiresAt, ct);
            return CreatedAtAction(nameof(GetById), new { id = credential.Id.Value }, credential);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Credential creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("v3")]
    public async Task<IActionResult> CreateV3(
        [FromBody] CreateV3Request request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var credential = await _service.CreateV3Async(
                request.Name, request.UserName, request.AuthProtocol,
                request.AuthPassword, request.PrivProtocol,
                request.PrivPassword, Actor,
                request.ExpiresAt, ct);
            return CreatedAtAction(nameof(GetById), new { id = credential.Id.Value }, credential);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Credential creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/rotate-v2c")]
    public async Task<IActionResult> RotateV2c(
        Guid id, [FromBody] RotateV2cRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _service.RotateV2cAsync(CredentialId.From(id), request.NewCommunity, Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Credential not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/rotate-v3-auth")]
    public async Task<IActionResult> RotateV3Auth(
        Guid id, [FromBody] RotateV3AuthRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _service.RotateV3AuthAsync(CredentialId.From(id), request.NewAuthPassword, Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Credential not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/rotate-v3-priv")]
    public async Task<IActionResult> RotateV3Priv(
        Guid id, [FromBody] RotateV3PrivRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _service.RotateV3PrivAsync(CredentialId.From(id), request.NewPrivPassword, Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Credential not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            await _service.DeactivateAsync(CredentialId.From(id), Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Credential not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    private string Actor => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated actor is unavailable.");
}
