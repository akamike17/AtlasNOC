using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface ITopologyService
{
    Task<TopologyMap> GetTopologyAsync(CancellationToken cancellationToken = default);
    Task<TopologyMap> GetTopologyForSubnetAsync(string subnetCidr, CancellationToken cancellationToken = default);
    Task<TopologyNode?> GetNodeAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopologyLink>> GetLinksAsync(CancellationToken cancellationToken = default);
    Task<TopologyPath?> FindPathAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
    Task<TopologyMap> RebuildTopologyAsync(CancellationToken cancellationToken = default);
}

public sealed record TopologyMap
(
    Guid Id,
    DateTime GeneratedAt,
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyLink> Links,
    TopologyMetadata Metadata
);

public sealed record TopologyNode
(
    Guid DeviceId,
    string IpAddress,
    string? Hostname,
    string? Vendor,
    DeviceType? DeviceType,
    DeviceStatus Status,
    double X,
    double Y,
    IReadOnlyList<TopologyInterface> Interfaces
);

public sealed record TopologyInterface
(
    string IfIndex,
    string? Name,
    string? MacAddress,
    InterfaceAdminStatus AdminStatus,
    InterfaceOperStatus OperStatus,
    Guid? LinkId
);

public sealed record TopologyLink
(
    Guid Id,
    Guid SourceNodeId,
    Guid TargetNodeId,
    string SourceInterface,
    string TargetInterface,
    LinkType Type,
    double Confidence,
    LinkStatus Status
);

public sealed record TopologyPath
(
    IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<Guid> LinkIds,
    double TotalCost,
    bool IsComplete
);

public sealed record TopologyMetadata
(
    int TotalDevices,
    int OnlineDevices,
    int OfflineDevices,
    int TotalLinks,
    int ConfirmedLinks,
    int InferredLinks,
    double AverageConfidence
);

public enum LinkType
{
    Lldp = 1,
    Cdp = 2,
    Arp = 3,
    MacTable = 4,
    Manual = 5,
    Inferred = 6
}

public enum LinkStatus
{
    Up = 1,
    Down = 2,
    Degraded = 3,
    Unknown = 4
}