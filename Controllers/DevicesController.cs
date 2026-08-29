using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReadOnly")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _service;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceService service, ILogger<DevicesController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IReadOnlyList<Device>> GetAll(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return string.IsNullOrWhiteSpace(search)
            ? await _service.GetAllAsync(ct)
            : await _service.SearchAsync(search, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var device = await _service.GetByIdAsync(DeviceId.From(id), ct);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDeviceRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var device = await _service.CreateAsync(
                request.Name, request.IpAddress, request.Type,
                Actor, request.Location, request.Description, ct);
            return CreatedAtAction(nameof(GetById), new { id = device.Id.Value }, device);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Device creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            _logger.LogInformation(ex, "Device creation rejected because IP {IpAddress} already exists", request.IpAddress);
            return Conflict(new { error = "A device with this IP address already exists." });
        }
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var device = await _service.UpdateStatusAsync(
                DeviceId.From(id), request.Status, Actor, ct);
            return Ok(device);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Device not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/details")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> UpdateDetails(
        Guid id, [FromBody] UpdateDetailsRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var device = await _service.UpdateDetailsAsync(
                DeviceId.From(id), request.Name, request.Location,
                request.Description, Actor, ct);
            return Ok(device);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Device not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/deactivate")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            await _service.DeactivateAsync(DeviceId.From(id), Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Device not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/reactivate")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Reactivate(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            await _service.ReactivateAsync(DeviceId.From(id), Actor, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Device not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("down")]
    public async Task<IReadOnlyList<Device>> GetDownDevices(CancellationToken ct = default)
        => await _service.GetDownDevicesAsync(ct);

    private string Actor => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated actor is unavailable.");
}
