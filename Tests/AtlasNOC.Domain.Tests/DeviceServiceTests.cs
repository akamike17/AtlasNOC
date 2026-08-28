using System;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public class DeviceServiceTests
{
    private readonly InMemoryRepository<Device> _repo;
    private readonly TestLogger<AuditService> _auditLogger;
    private readonly InMemoryRepository<AuditEvent> _auditRepo;
    private readonly AuditService _auditService;
    private readonly DeviceService _deviceService;

    public DeviceServiceTests()
    {
        _repo = new InMemoryRepository<Device>(d => d.Id.Value);
        _auditRepo = new InMemoryRepository<AuditEvent>(e => e.EventId);
        _auditLogger = new TestLogger<AuditService>();
        _auditService = new AuditService(_auditRepo, _auditLogger);
        _deviceService = new DeviceService(_repo, _auditService);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDevice_WithCorrectProperties()
    {
        var device = await _deviceService.CreateAsync(
            "Core-Router-01", "192.168.1.1", DeviceType.Router, "admin",
            location: "DataCenter-A", description: "Primary core router");

        Assert.NotNull(device);
        Assert.NotEqual(Guid.Empty, device.Id.Value);
        Assert.Equal("Core-Router-01", device.Name);
        Assert.Equal("192.168.1.1", device.IpAddress);
        Assert.Equal(DeviceType.Router, device.Type);
        Assert.Equal(DeviceStatus.Unknown, device.Status);
        Assert.Equal("DataCenter-A", device.Location);
        Assert.Equal("Primary core router", device.Description);
        Assert.True(device.IsActive);
        Assert.Equal("admin", device.CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidName_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _deviceService.CreateAsync("", "192.168.1.1", DeviceType.Router, "admin"));
    }

    [Fact]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _deviceService.CreateAsync(null!, "192.168.1.1", DeviceType.Router, "admin"));
    }

    [Fact]
    public async Task GetByIdAsync_FoundDevice_ShouldReturnDevice()
    {
        var created = await _deviceService.CreateAsync(
            "Test-Switch", "192.168.1.10", DeviceType.Switch, "admin");
        var found = await _deviceService.GetByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldReturnNull()
    {
        var found = await _deviceService.GetByIdAsync(
            DeviceId.From(Guid.NewGuid()));
        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldChangeStatus_AndAudit()
    {
        var device = await _deviceService.CreateAsync(
            "Test-Router", "192.168.1.1", DeviceType.Router, "admin");
        var updated = await _deviceService.UpdateStatusAsync(
            device.Id, DeviceStatus.Up, "operator");
        Assert.Equal(DeviceStatus.Up, updated.Status);
        Assert.NotNull(updated.LastCheckedAt);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldSetIsActiveToFalse()
    {
        var device = await _deviceService.CreateAsync(
            "Test-Switch", "192.168.1.10", DeviceType.Switch, "admin");
        await _deviceService.DeactivateAsync(device.Id, "admin");
        var found = await _deviceService.GetByIdAsync(device.Id);
        Assert.NotNull(found);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingDevices()
    {
        await _deviceService.CreateAsync("Router-A", "192.168.1.1", DeviceType.Router, "admin");
        await _deviceService.CreateAsync("Switch-B", "192.168.1.2", DeviceType.Switch, "admin");
        await _deviceService.CreateAsync("Router-C", "192.168.1.3", DeviceType.Router, "admin");
        var results = await _deviceService.SearchAsync("router");
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Equal(DeviceType.Router, d.Type));
    }

    [Fact]
    public async Task GetDownDevicesAsync_ShouldReturnOnlyDownDevices()
    {
        var d1 = await _deviceService.CreateAsync("Router-A", "192.168.1.1", DeviceType.Router, "admin");
        var d2 = await _deviceService.CreateAsync("Router-B", "192.168.1.2", DeviceType.Router, "admin");
        await _deviceService.UpdateStatusAsync(d1.Id, DeviceStatus.Down, "admin");
        await _deviceService.UpdateStatusAsync(d2.Id, DeviceStatus.Up, "admin");
        var downDevices = await _deviceService.GetDownDevicesAsync();
        Assert.Single(downDevices);
        Assert.Equal(d1.Id, downDevices.First().Id);
    }

    [Fact]
    public async Task AuditLog_ShouldBeCreated_WhenDeviceCreated()
    {
        await _deviceService.CreateAsync("Audit-Test", "192.168.1.1", DeviceType.Router, "admin");
        var logs = _auditLogger.Logs;
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Message.Contains("Create") && l.Message.Contains("Device"));
    }
}
