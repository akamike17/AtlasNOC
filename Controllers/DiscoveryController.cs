using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReadOnly")]
public class DiscoveryController : ControllerBase
{
    private readonly IDiscoveryService _service;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(IDiscoveryService service, ILogger<DiscoveryController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start network discovery for a CIDR subnet.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Discover(
        [FromBody] DiscoverRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var domainRequest = new DiscoveryRequest(
                request.SubnetCidr,
                request.CredentialIds?.Select(g => CredentialId.From(g)) ?? Array.Empty<CredentialId>(),
                new DiscoveryOptions(
                    request.MaxConcurrency ?? 50,
                    request.PingTimeout ?? TimeSpan.FromSeconds(2),
                    request.SnmpTimeout ?? TimeSpan.FromSeconds(5),
                    request.EnableLldp,
                    request.EnableCdp,
                    request.EnableArp,
                    request.CommonPorts ?? Array.Empty<int>()
                )
            );

            var result = await _service.DiscoverAsync(domainRequest, ct);

            return Ok(new
            {
                result.Id,
                result.StartedAt,
                result.CompletedAt,
                result.Status,
                result.TargetsScanned,
                result.TargetsReachable,
                DeviceCount = result.Devices.Count,
                result.ErrorMessage
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Discovery failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Discovery cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery failed unexpectedly");
            return StatusCode(500, new { error = "Discovery failed" });
        }
    }

    /// <summary>
    /// Get discovered devices from the most recent discovery.
    /// </summary>
    [HttpGet("devices")]
    public async Task<IReadOnlyList<DiscoveredDevice>> GetDiscoveredDevices(CancellationToken ct = default)
        => await _service.GetDiscoveredDevicesAsync(ct);

    /// <summary>
    /// Get a specific discovered device by ID.
    /// </summary>
    [HttpGet("devices/{id:guid}")]
    public async Task<IActionResult> GetDiscoveredDevice(Guid id, CancellationToken ct = default)
    {
        var device = await _service.GetDiscoveredDeviceAsync(id, ct);
        return device is null ? NotFound() : Ok(device);
    }
}

public class DiscoverRequest
{
    public string SubnetCidr { get; set; } = "";
    public IEnumerable<Guid>? CredentialIds { get; set; }
    public int? MaxConcurrency { get; set; }
    public TimeSpan? PingTimeout { get; set; }
    public TimeSpan? SnmpTimeout { get; set; }
    public bool EnableLldp { get; set; } = true;
    public bool EnableCdp { get; set; } = true;
    public bool EnableArp { get; set; } = true;
    public IEnumerable<int>? CommonPorts { get; set; }
}
