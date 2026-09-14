namespace AtlasNOC.Application.Wisp;

/// <summary>Capacidades declaradas por una integración WISP; no implica que esté configurada.</summary>
[Flags]
public enum WispConnectorCapabilities
{
    None = 0,
    ReadClients = 1,
    ReadSessions = 2,
    ReadCpe = 4,
    ReadTopology = 8,
    SuspendClient = 16
}

public sealed record WispConnectorDescriptor(
    string Key,
    string DisplayName,
    WispConnectorCapabilities Capabilities,
    bool RequiresCredentials,
    bool ReadOnlyByDefault);

public sealed record WispClientEvidence(
    string ExternalId,
    string? AccountReference,
    string? CpeAddress,
    string? SessionReference,
    DateTime ObservedAtUtc,
    string Source,
    double Confidence);

/// <summary>
/// Contrato neutral para consultar sistemas WISP autorizados. Las acciones mutantes
/// requieren una capacidad separada y nunca se ejecutan durante Discovery.
/// </summary>
public interface IWispConnector
{
    WispConnectorDescriptor Descriptor { get; }
    bool IsConfigured { get; }

    Task<IReadOnlyList<WispClientEvidence>> ReadClientsAsync(
        CancellationToken ct = default);
}

public interface IWispConnectorRegistry
{
    IReadOnlyList<WispConnectorDescriptor> List();
    IWispConnector? Resolve(string key);
}

public static class WispConnectorCatalog
{
    public static IReadOnlyList<WispConnectorDescriptor> Supported { get; } =
    [
        new("radius", "RADIUS / PPPoE", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadSessions, true, true),
        new("mikrotik", "MikroTik RouterOS", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadSessions | WispConnectorCapabilities.ReadTopology, true, true),
        new("aruba", "Aruba Central", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadTopology, true, true),
        new("cisco", "Cisco RESTCONF", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadTopology, true, true),
        new("ubiquiti", "Ubiquiti UniFi", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadTopology, true, true),
        new("olt", "OLT / ONT", WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadCpe, true, true),
        new("tr069-usp", "TR-069 / USP", WispConnectorCapabilities.ReadCpe, true, true)
    ];
}
