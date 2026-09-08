using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Enlace real entre dos interfaces. Solo se crea con evidencia suficiente.</summary>
public class NetworkLink
{
    public LinkId Id { get; private set; } = null!;
    public InterfaceId AInterfaceId { get; private set; } = null!;
    public InterfaceId BInterfaceId { get; private set; } = null!;
    public LinkType LinkType { get; private set; }
    public DiscoverySource DiscoverySource { get; private set; }
    public double Confidence { get; private set; }
    public InterfaceAdminStatus AdminStatus { get; private set; }
    public InterfaceOperStatus OperStatus { get; private set; }
    public ulong? CapacityBps { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }
    public bool IsConfirmed { get; private set; }
    public bool IsStale { get; private set; }
    public bool IsManual { get; private set; }

    private NetworkLink() { }

    public NetworkLink(InterfaceId aInterfaceId, InterfaceId bInterfaceId,
        LinkType linkType, DiscoverySource discoverySource, double confidence,
        bool isManual = false, ulong? capacityBps = null)
    {
        if (aInterfaceId == bInterfaceId)
            throw new ArgumentException("A link cannot connect an interface to itself.");

        ArgumentNullException.ThrowIfNull(aInterfaceId);
        ArgumentNullException.ThrowIfNull(bInterfaceId);
        Id = LinkId.New();
        // Representación canónica: A siempre es el GUID menor. Así A-B y B-A
        // producen exactamente la misma clave única en base de datos.
        if (aInterfaceId.Value.CompareTo(bInterfaceId.Value) < 0)
        {
            AInterfaceId = aInterfaceId;
            BInterfaceId = bInterfaceId;
        }
        else
        {
            AInterfaceId = bInterfaceId;
            BInterfaceId = aInterfaceId;
        }
        LinkType = linkType;
        DiscoverySource = discoverySource;
        Confidence = confidence;
        IsManual = isManual;
        IsConfirmed = ShouldAutoConfirm(isManual, discoverySource, confidence);
        CapacityBps = capacityBps;
        AdminStatus = InterfaceAdminStatus.Unknown;
        OperStatus = InterfaceOperStatus.Unknown;
        LastSeenAtUtc = DateTime.UtcNow;
    }

    public void MarkStale(bool stale = true)
    {
        if (!IsManual) IsStale = stale;
    }
    public void Confirm() => IsConfirmed = true;
    public void MarkSeen(DateTime? atUtc = null)
    {
        LastSeenAtUtc = atUtc ?? DateTime.UtcNow;
        IsStale = false;
    }

    public void RefreshEvidence(LinkType linkType, DiscoverySource discoverySource, double confidence,
        DateTime? atUtc = null)
    {
        if (IsManual) { MarkSeen(atUtc); return; }
        if (confidence > Confidence)
        {
            Confidence = confidence;
            LinkType = linkType;
            DiscoverySource = discoverySource;
        }
        IsConfirmed = IsConfirmed || ShouldAutoConfirm(false, discoverySource, confidence);
        MarkSeen(atUtc);
    }

    private static bool ShouldAutoConfirm(bool isManual, DiscoverySource source, double confidence)
        => isManual || source switch
        {
            DiscoverySource.Lldp or DiscoverySource.Cdp => confidence >= 0.90,
            DiscoverySource.WirelessAssociation => confidence >= 0.90,
            DiscoverySource.MikroTikNeighbor or DiscoverySource.Ubiquiti => confidence >= 0.90,
            _ => false
        };
}
