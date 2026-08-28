using System;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public class AlertServiceTests
{
    private readonly InMemoryRepository<Alert> _repo;
    private readonly TestLogger<AuditService> _auditLogger;
    private readonly InMemoryRepository<AuditEvent> _auditRepo;
    private readonly AuditService _auditService;
    private readonly AlertService _alertService;

    public AlertServiceTests()
    {
        _repo = new InMemoryRepository<Alert>(a => a.Id.Value);
        _auditRepo = new InMemoryRepository<AuditEvent>(e => e.EventId);
        _auditLogger = new TestLogger<AuditService>();
        _auditService = new AuditService(_auditRepo, _auditLogger);
        _alertService = new AlertService(_repo, _auditService);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAlert_WithCorrectProperties()
    {
        var deviceId = DeviceId.New();
        var request = new CreateAlertRequest(deviceId, "Interface down", AlertSeverity.Critical, "test", null);
        var alert = await _alertService.CreateAsync(request);

        Assert.NotNull(alert);
        Assert.NotEqual(Guid.Empty, alert.Id.Value);
        Assert.Equal(deviceId, alert.DeviceId);
        Assert.Equal("Interface down", alert.Message);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.True(alert.IsActive);
        Assert.Null(alert.AcknowledgedAt);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public async Task AcknowledgeAsync_ShouldSetAcknowledgedAt()
    {
        var request = new CreateAlertRequest(DeviceId.New(), "Test alert", AlertSeverity.Medium, "test", null);
        var alert = await _alertService.CreateAsync(request);
        await _alertService.AcknowledgeAsync(alert.Id, "operator");

        var acknowledged = await _alertService.GetByIdAsync(alert.Id);
        Assert.NotNull(acknowledged);
        Assert.NotNull(acknowledged.AcknowledgedAt);
        Assert.Equal("operator", acknowledged.AcknowledgedBy);
    }

    [Fact]
    public async Task ResolveAsync_ShouldSetResolvedAt_AndNotes()
    {
        var request = new CreateAlertRequest(DeviceId.New(), "Test alert", AlertSeverity.High, "test", null);
        var alert = await _alertService.CreateAsync(request);
        await _alertService.ResolveAsync(alert.Id, "operator", "Fixed the issue");

        var resolved = await _alertService.GetByIdAsync(alert.Id);
        Assert.NotNull(resolved);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal("operator", resolved.ResolvedBy);
        Assert.Equal("Fixed the issue", resolved.ResolutionNotes);
        Assert.False(resolved.IsActive);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldReturnOnlyActiveAlerts()
    {
        var deviceId = DeviceId.New();

        var activeRequest = new CreateAlertRequest(deviceId, "Active alert", AlertSeverity.Medium, "test", null);
        var activeAlert = await _alertService.CreateAsync(activeRequest);

        var resolvedRequest = new CreateAlertRequest(deviceId, "Resolved alert", AlertSeverity.High, "test", null);
        var resolvedAlert = await _alertService.CreateAsync(resolvedRequest);
        await _alertService.ResolveAsync(resolvedAlert.Id, "operator");

        var active = await _alertService.GetActiveAlertsAsync();

        Assert.Single(active);
        Assert.Equal(activeAlert.Id, active[0].Id);
    }

    [Fact]
    public async Task GetAlertsForDeviceAsync_ShouldReturnOnlyDeviceAlerts()
    {
        var deviceId1 = DeviceId.New();
        var deviceId2 = DeviceId.New();

        var request1 = new CreateAlertRequest(deviceId1, "Alert for device 1", AlertSeverity.Medium, "test", null);
        var request2 = new CreateAlertRequest(deviceId2, "Alert for device 2", AlertSeverity.High, "test", null);

        await _alertService.CreateAsync(request1);
        await _alertService.CreateAsync(request2);

        var device1Alerts = await _alertService.GetAlertsForDeviceAsync(deviceId1);

        Assert.Single(device1Alerts);
        Assert.Equal(deviceId1, device1Alerts[0].DeviceId);
    }

    [Fact]
    public async Task GetCriticalAlertsAsync_ShouldReturnOnlyHighAndCritical()
    {
        var deviceId = DeviceId.New();

        var lowRequest = new CreateAlertRequest(deviceId, "Low alert", AlertSeverity.Low, "test", null);
        var mediumRequest = new CreateAlertRequest(deviceId, "Medium alert", AlertSeverity.Medium, "test", null);
        var highRequest = new CreateAlertRequest(deviceId, "High alert", AlertSeverity.High, "test", null);
        var criticalRequest = new CreateAlertRequest(deviceId, "Critical alert", AlertSeverity.Critical, "test", null);

        await _alertService.CreateAsync(lowRequest);
        await _alertService.CreateAsync(mediumRequest);
        await _alertService.CreateAsync(highRequest);
        await _alertService.CreateAsync(criticalRequest);

        var critical = await _alertService.GetCriticalAlertsAsync();

        Assert.Equal(2, critical.Count);
        Assert.All(critical, a => Assert.True(a.Severity >= AlertSeverity.High));
    }

    [Fact]
    public async Task GetUnacknowledgedAlertsAsync_ShouldReturnOnlyUnacknowledged()
    {
        var deviceId = DeviceId.New();

        var ackedRequest = new CreateAlertRequest(deviceId, "Acknowledged alert", AlertSeverity.Medium, "test", null);
        var ackedAlert = await _alertService.CreateAsync(ackedRequest);
        await _alertService.AcknowledgeAsync(ackedAlert.Id, "operator");

        var unackedRequest = new CreateAlertRequest(deviceId, "Unacknowledged alert", AlertSeverity.High, "test", null);
        var unackedAlert = await _alertService.CreateAsync(unackedRequest);

        var unacknowledged = await _alertService.GetUnacknowledgedAlertsAsync();

        Assert.Single(unacknowledged);
        Assert.Equal(unackedAlert.Id, unacknowledged[0].Id);
    }

    [Fact]
    public async Task CreateAsync_DeduplicatesSameActiveDeviceCondition()
    {
        var deviceId = DeviceId.New();
        var request = new CreateAlertRequest(deviceId, "Device down", AlertSeverity.High, "polling", null);

        var first = await _alertService.CreateAsync(request);
        var duplicate = await _alertService.CreateAsync(request with { Source = "polling-retry" });

        Assert.Equal(first.Id, duplicate.Id);
        Assert.Single(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task CreateAsync_AllowsConditionAgainAfterRecoveryResolution()
    {
        var deviceId = DeviceId.New();
        var request = new CreateAlertRequest(deviceId, "Device down", AlertSeverity.High, "polling", null);
        var first = await _alertService.CreateAsync(request);
        await _alertService.ResolveAsync(first.Id, "system", "Recovered");

        var recurrence = await _alertService.CreateAsync(request);

        Assert.NotEqual(first.Id, recurrence.Id);
        Assert.Equal(2, (await _repo.GetAllAsync()).Count);
    }
}
