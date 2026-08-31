namespace AtlasNOC.Web.Security;

/// <summary>
/// Scopes de autorización para API keys. Cada endpoint de <c>/api/*</c> exige el
/// scope correspondiente; las keys se crean con la lista de scopes que poseen.
/// Véase especificación §2.
/// </summary>
public static class ApiScopes
{
    public const string TopologyRead = "topology.read";
    public const string MetricsRead = "metrics.read";
    public const string DevicesRead = "devices.read";
    public const string DevicesWrite = "devices.write";
    public const string SitesRead = "sites.read";
    public const string SitesWrite = "sites.write";
    public const string DiscoveryRun = "discovery.run";
    public const string AlertsRead = "alerts.read";
    public const string AlertsWrite = "alerts.write";
    public const string IncidentsRead = "incidents.read";
    public const string IncidentsWrite = "incidents.write";
    public const string SystemRead = "system.read";

    /// <summary>Todos los scopes conocidos, en orden canónico.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        TopologyRead, MetricsRead, DevicesRead, DevicesWrite, SitesRead, SitesWrite,
        DiscoveryRun, AlertsRead, AlertsWrite, IncidentsRead, IncidentsWrite, SystemRead,
    };
}