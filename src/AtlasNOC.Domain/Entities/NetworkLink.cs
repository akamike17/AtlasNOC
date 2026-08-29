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

        Id = LinkId.New();
        AInterfaceId = aInterfaceId ?? throw new ArgumentNullException(nameof(aInterfaceId));
        BInterfaceId = bInterfaceId ?? throw new ArgumentNullException(nameof(bInterfaceId));
        LinkType = linkType;
        DiscoverySource = discoverySource;
        Confidence = confidence;
        IsManual = isManual;
        IsConfirmed = isManual || confidence >= 0.5;
        CapacityBps = capacityBps;
        AdminStatus = InterfaceAdminStatus.Up;
        OperStatus = InterfaceOperStatus.Up;
        LastSeenAtUtc = DateTime.UtcNow;
    }

    public void MarkStale(bool stale = true) => IsStale = stale;
    public void Confirm() => IsConfirmed = true;
    public void MarkSeen() => LastSeenAtUtc = DateTime.UtcNow;
}