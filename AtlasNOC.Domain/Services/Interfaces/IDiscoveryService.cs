using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IDiscoveryService
{
    Task<DiscoveryResult> DiscoverAsync(DiscoveryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiscoveredDevice>> GetDiscoveredDevicesAsync(CancellationToken cancellationToken = default);
    Task<DiscoveredDevice?> GetDiscoveredDeviceAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record DiscoveryRequest
(
    string SubnetCidr,
    IEnumerable<CredentialId> CredentialIds,
    DiscoveryOptions Options
);

public sealed record DiscoveryOptions
(
    int MaxConcurrency = 50,
    TimeSpan PingTimeout = default,
    TimeSpan SnmpTimeout = default,
    bool EnableLldp = true,
    bool EnableCdp = true,
    bool EnableArp = true,
    IEnumerable<int> CommonPorts = default!,
    int MaxTargets = 4096
);

public sealed record DiscoveryResult
(
    Guid Id,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DiscoveryStatus Status,
    IReadOnlyList<DiscoveredDevice> Devices,
    int TargetsScanned,
    int TargetsReachable,
    string? ErrorMessage
);

public sealed record DiscoveredDevice
(
    Guid Id,
    string IpAddress,
    string? Hostname,
    string? SysDescr,
    string? SysObjectId,
    string? Vendor,
    DeviceType? DeviceType,
    IReadOnlyList<DiscoveredInterface> Interfaces,
    IReadOnlyList<DiscoveredNeighbor> Neighbors,
    DateTime DiscoveredAt,
    DiscoveryEvidence Evidence
);

public sealed record DiscoveredInterface
(
    string IfIndex,
    string? Name,
    string? Description,
    string? MacAddress,
    string? IpAddress,
    InterfaceAdminStatus AdminStatus,
    InterfaceOperStatus OperStatus,
    long? Speed,
    string? Alias,
    IReadOnlyList<VlanInfo> Vlans
);

public sealed record DiscoveredNeighbor
(
    string LocalInterface,
    string RemoteChassisId,
    string RemotePortId,
    string? RemoteSystemName,
    NeighborProtocol Protocol,
    double Confidence
);

public enum DiscoveryStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum InterfaceAdminStatus
{
    Up = 1,
    Down = 2,
    Testing = 3
}

public enum InterfaceOperStatus
{
    Up = 1,
    Down = 2,
    Testing = 3,
    Unknown = 4,
    Dormant = 5,
    NotPresent = 6,
    LowerLayerDown = 7
}

public enum NeighborProtocol
{
    Lldp = 1,
    Cdp = 2,
    Arp = 3,
    MacTable = 4
}

public sealed record DiscoveryEvidence
(
    bool HasPing,
    bool HasSnmp,
    bool HasLldp,
    bool HasCdp,
    bool HasArp,
    bool HasMacTable,
    IReadOnlyList<string> OidsQueried,
    IReadOnlyList<string> OidsResponded
);

public sealed record VlanInfo
(
    int VlanId,
    string? Name,
    bool IsTagged
);
