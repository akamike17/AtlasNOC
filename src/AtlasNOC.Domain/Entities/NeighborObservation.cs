using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Evidencia cruda de un vecino antes de convertirse en enlace.</summary>
public class NeighborObservation
{
    public Guid Id { get; private set; }
    public DeviceId LocalDeviceId { get; private set; } = null!;
    public InterfaceId LocalInterfaceId { get; private set; } = null!;
    public string RemoteIdentity { get; private set; } = string.Empty;
    public string? RemotePortIdentity { get; private set; }
    public NeighborProtocol Protocol { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public string RawEvidenceHash { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    private NeighborObservation() { }

    public NeighborObservation(DeviceId localDeviceId, InterfaceId localInterfaceId,
        string remoteIdentity, NeighborProtocol protocol, string rawEvidenceHash,
        string? remotePortIdentity = null)
    {
        Id = Guid.NewGuid();
        LocalDeviceId = localDeviceId ?? throw new ArgumentNullException(nameof(localDeviceId));
        LocalInterfaceId = localInterfaceId ?? throw new ArgumentNullException(nameof(localInterfaceId));
        RemoteIdentity = remoteIdentity ?? throw new ArgumentNullException(nameof(remoteIdentity));
        RemotePortIdentity = remotePortIdentity;
        Protocol = protocol;
        RawEvidenceHash = rawEvidenceHash ?? throw new ArgumentNullException(nameof(rawEvidenceHash));
        ObservedAtUtc = DateTime.UtcNow;
    }

    public void Resolve() => IsResolved = true;
}