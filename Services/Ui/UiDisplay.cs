using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Services.Ui;

/// <summary>Presentation helpers for the Razor views (labels, badge classes, dates).</summary>
public static class UiDisplay
{
    public static string SevText(AlertSeverity s) => s.ToString();

    public static string SevClass(AlertSeverity s) => s switch
    {
        AlertSeverity.Critical => "Critical",
        AlertSeverity.High => "High",
        AlertSeverity.Medium => "Medium",
        AlertSeverity.Low => "Low",
        AlertSeverity.Info => "Secondary",
        _ => "Unknown"
    };

    public static string StatusText(DeviceStatus s) => s switch
    {
        DeviceStatus.Up => "Up",
        DeviceStatus.Down => "Down",
        DeviceStatus.Maintenance => "Mantenimiento",
        DeviceStatus.Snooping => "Snooping",
        DeviceStatus.Test => "Prueba",
        _ => "Desconocido"
    };

    public static string StatusClass(DeviceStatus s) => s switch
    {
        DeviceStatus.Up => "Up",
        DeviceStatus.Down => "Down",
        _ => "Unknown"
    };

    public static string StatusDot(DeviceStatus s) => s switch
    {
        DeviceStatus.Up => "up",
        DeviceStatus.Down => "down",
        DeviceStatus.Maintenance or DeviceStatus.Snooping or DeviceStatus.Test => "maintenance",
        _ => "unknown"
    };

    public static string TypeText(DeviceType t) => t switch
    {
        DeviceType.Router => "Router",
        DeviceType.Switch => "Switch",
        DeviceType.Firewall => "Firewall",
        DeviceType.Server => "Servidor",
        DeviceType.AccessPoint => "Access Point",
        DeviceType.Printer => "Impresora",
        DeviceType.IoT => "IoT",
        DeviceType.Other => "Otro",
        _ => "Desconocido"
    };

    public static string IncidentStatusText(IncidentStatus s) => s switch
    {
        IncidentStatus.New => "Nuevo",
        IncidentStatus.Investigating => "Investigando",
        IncidentStatus.Monitoring => "Monitoreando",
        IncidentStatus.Resolved => "Resuelto",
        IncidentStatus.Closed => "Cerrado",
        IncidentStatus.Reopened => "Reabierto",
        _ => s.ToString()
    };

    public static string DiscoveryStatusText(DiscoveryStatus s) => s switch
    {
        DiscoveryStatus.Pending => "Pendiente",
        DiscoveryStatus.Running => "En ejecución",
        DiscoveryStatus.Completed => "Completado",
        DiscoveryStatus.Failed => "Fallido",
        DiscoveryStatus.Cancelled => "Cancelado",
        _ => s.ToString()
    };

    public static string LinkTypeText(LinkType t) => t.ToString();
    public static string LinkStatusText(LinkStatus s) => s.ToString();

    public static string FormatDateTime(DateTime? dt)
    {
        if (!dt.HasValue) return "—";
        var local = dt.Value.ToLocalTime();
        return local.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string FormatAge(DateTime dt)
    {
        var age = DateTime.UtcNow - dt;
        if (age < TimeSpan.FromMinutes(1)) return "ahora";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours} h";
        return $"{(int)age.TotalDays} d";
    }

    public static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}