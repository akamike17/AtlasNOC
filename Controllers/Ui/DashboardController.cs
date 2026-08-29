using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class DashboardController : Controller
{
    private readonly IDeviceService _deviceService;
    private readonly IAlertService _alertService;
    private readonly IIncidentService _incidentService;
    private readonly ICveService _cveService;
    private readonly ITopologyService _topologyService;
    private readonly IRepository<DiscoveryRun> _runRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDeviceService deviceService, IAlertService alertService,
        IIncidentService incidentService, ICveService cveService,
        ITopologyService topologyService, IRepository<DiscoveryRun> runRepository,
        ILogger<DashboardController> logger)
    {
        _deviceService = deviceService;
        _alertService = alertService;
        _incidentService = incidentService;
        _cveService = cveService;
        _topologyService = topologyService;
        _runRepository = runRepository;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var model = await BuildAsync(ct);
        return View(model);
    }

    /// <summary>Lightweight summary used by the dashboard's periodic refresh (no page reload).</summary>
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        try
        {
            var devices = await _deviceService.GetAllAsync(ct);
            var active = await _alertService.GetActiveAlertsAsync(ct);
            var critical = await _alertService.GetCriticalAlertsAsync(ct);
            var open = await _incidentService.GetOpenAsync(ct);

            var lastPolled = devices.Where(d => d.LastCheckedAt.HasValue)
                .Select(d => d.LastCheckedAt!.Value).DefaultIfEmpty().Max();

            return Json(new
            {
                totalDevices = devices.Count,
                up = devices.Count(d => d.Status == DeviceStatus.Up),
                down = devices.Count(d => d.Status == DeviceStatus.Down),
                unknown = devices.Count(d => d.Status is DeviceStatus.Unknown or DeviceStatus.Test or DeviceStatus.Snooping),
                activeAlerts = active.Count,
                criticalAlerts = critical.Count,
                openIncidents = open.Count,
                lastPolled = lastPolled == default ? (DateTime?)null : lastPolled,
                generatedAt = DateTime.UtcNow
            });
        }
        catch (Exception)
        {
            return StatusCode(503, new { error = "Resumen no disponible" });
        }
    }

    private async Task<DashboardViewModel> BuildAsync(CancellationToken ct)
    {
        IReadOnlyList<Device> devices = Array.Empty<Device>();
        IReadOnlyList<Alert> activeAlerts = Array.Empty<Alert>();
        IReadOnlyList<Alert> criticalAlerts = Array.Empty<Alert>();
        IReadOnlyList<Incident> openIncidents = Array.Empty<Incident>();
        IReadOnlyList<DiscoveryRun> runs = Array.Empty<DiscoveryRun>();
        var mysqlHealthy = false;

        try
        {
            devices = await _deviceService.GetAllAsync(ct);
            mysqlHealthy = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard: unable to load devices");
        }

        try { activeAlerts = await _alertService.GetActiveAlertsAsync(ct); } catch (Exception ex) { _logger.LogWarning(ex, "Dashboard alerts"); }
        try { criticalAlerts = await _alertService.GetCriticalAlertsAsync(ct); } catch (Exception ex) { _logger.LogWarning(ex, "Dashboard critical alerts"); }
        try { openIncidents = await _incidentService.GetOpenAsync(ct); } catch (Exception ex) { _logger.LogWarning(ex, "Dashboard incidents"); }
        try { runs = (await _runRepository.GetAllAsync(ct)).OrderByDescending(r => r.StartedAt).Take(5).ToList(); } catch (Exception ex) { _logger.LogWarning(ex, "Dashboard runs"); }

        var now = DateTime.UtcNow;
        var activity = new List<DashboardActivityItem>();

        foreach (var d in devices.Where(x => x.LastCheckedAt.HasValue)
                     .OrderByDescending(x => x.LastCheckedAt).Take(6))
        {
            activity.Add(new DashboardActivityItem
            {
                Kind = "device",
                Label = d.Name,
                Subtext = d.Status == DeviceStatus.Up ? "Respondiendo" : $"Estado: {StatusText(d.Status)}",
                At = d.LastCheckedAt!.Value,
                SeverityBadge = d.Status == DeviceStatus.Up ? "Success" : d.Status == DeviceStatus.Down ? "Danger" : "Warning",
                Link = $"/devices/{d.Id.Value}"
            });
        }

        foreach (var a in activeAlerts.OrderByDescending(a => a.OccurredAt).Take(5))
        {
            activity.Add(new DashboardActivityItem
            {
                Kind = "alert",
                Label = a.Message,
                Subtext = devices.FirstOrDefault(d => d.Id == a.DeviceId)?.Name ?? "dispositivo desconocido",
                At = a.OccurredAt,
                SeverityBadge = SevBadge(a.Severity),
                Link = $"/alerts"
            });
        }

        foreach (var i in openIncidents.OrderByDescending(i => i.CreatedAt).Take(5))
        {
            activity.Add(new DashboardActivityItem
            {
                Kind = "incident",
                Label = i.Title,
                Subtext = $"Incidente · {IncidentStatusText(i.Status)}",
                At = i.CreatedAt,
                SeverityBadge = "Info",
                Link = $"/incidents/{i.Id}"
            });
        }

        foreach (var r in runs.Take(5))
        {
            activity.Add(new DashboardActivityItem
            {
                Kind = "discovery",
                Label = $"Descubrimiento {r.SubnetCidr}",
                Subtext = $"{r.Status} · {r.TargetsReachable} alcanzables",
                At = r.StartedAt,
                SeverityBadge = "Info",
                Link = "/discovery"
            });
        }

        var orderedActivity = activity.OrderByDescending(a => a.At).Take(12).ToList();

        var total = devices.Count;
        var up = devices.Count(d => d.Status == DeviceStatus.Up);
        var down = devices.Count(d => d.Status == DeviceStatus.Down);
        var unknown = devices.Count(d => d.Status is DeviceStatus.Unknown or DeviceStatus.Test or DeviceStatus.Snooping);
        var inactive = devices.Count(d => !d.IsActive);

        var lastPolled = devices.Where(d => d.LastCheckedAt.HasValue).Select(d => d.LastCheckedAt!.Value).DefaultIfEmpty().Max();
        var lastUpdate = new DateTime?[]
            {
                lastPolled,
                activeAlerts.Select(a => (DateTime?)a.OccurredAt).DefaultIfEmpty().Max(),
                openIncidents.Select(i => (DateTime?)i.CreatedAt).DefaultIfEmpty().Max(),
                runs.Select(r => (DateTime?)r.StartedAt).DefaultIfEmpty().Max()
            }.Max();

        var cveStats = (CveStats?)null;
        IReadOnlyList<CveRecord> cveSamples = Array.Empty<CveRecord>();
        try
        {
            cveStats = await _cveService.GetStatsAsync(ct);
            cveSamples = await _cveService.GetCriticalAsync(ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Dashboard cve"); }

        var topologyNodes = 0;
        var topologyLinks = 0;
        var topologyUp = 0;
        var topologyDown = 0;
        var topologyKnown = false;
        try
        {
            var topo = await _topologyService.GetTopologyAsync(ct);
            topologyKnown = true;
            topologyNodes = topo.Nodes.Count;
            topologyLinks = topo.Links.Count;
            topologyUp = topo.Nodes.Count(n => n.Status == DeviceStatus.Up);
            topologyDown = topo.Nodes.Count(n => n.Status == DeviceStatus.Down);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Dashboard topology"); }

        return new DashboardViewModel
        {
            TotalDevices = total,
            Up = up,
            Down = down,
            Unknown = unknown,
            Inactive = inactive,
            ActiveAlerts = activeAlerts.Count,
            CriticalAlerts = criticalAlerts.Count,
            OpenIncidents = openIncidents.Count,
            CriticalCves = cveStats?.Critical ?? 0,
            TotalCves = cveStats?.Total ?? 0,
            CveStats = cveStats,
            TopologyKnown = topologyKnown,
            TopologyNodes = topologyNodes,
            TopologyLinks = topologyLinks,
            TopologyUp = topologyUp,
            TopologyDown = topologyDown,
            MySqlHealthy = mysqlHealthy,
            LastPolledAt = lastPolled == default ? null : lastPolled,
            LastDataUpdate = lastUpdate == default ? null : lastUpdate,
            RecentRuns = runs,
            Activity = orderedActivity,
            CriticalCveSamples = cveSamples.Take(6).ToList(),
            DownDevices = devices.Where(d => d.Status == DeviceStatus.Down).Take(8).ToList()
        };
    }

    internal static string SevBadge(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "Danger",
        AlertSeverity.High => "Warning",
        AlertSeverity.Medium => "Info",
        AlertSeverity.Low => "Secondary",
        AlertSeverity.Info => "Secondary",
        _ => "Secondary"
    };

    internal static string StatusText(DeviceStatus status) => status switch
    {
        DeviceStatus.Up => "Up",
        DeviceStatus.Down => "Down",
        DeviceStatus.Maintenance => "Mantenimiento",
        DeviceStatus.Snooping => "Snooping",
        DeviceStatus.Test => "Prueba",
        _ => "Desconocido"
    };

    internal static string IncidentStatusText(IncidentStatus status) => status switch
    {
        IncidentStatus.New => "Nuevo",
        IncidentStatus.Investigating => "Investigando",
        IncidentStatus.Monitoring => "Monitoreando",
        IncidentStatus.Resolved => "Resuelto",
        IncidentStatus.Closed => "Cerrado",
        IncidentStatus.Reopened => "Reabierto",
        _ => status.ToString()
    };
}