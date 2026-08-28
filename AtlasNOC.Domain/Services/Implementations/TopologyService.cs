using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace AtlasNOC.Domain.Services;

public sealed class TopologyService : ITopologyService
{
    private readonly IRepository<Device> _deviceRepository;
    private readonly IDiscoveryService _discoveryService;
    private readonly ILogger<TopologyService> _logger;
    private TopologyMap? _cachedTopology;
    private DateTime _lastRebuild = DateTime.MinValue;

    public TopologyService(
        IRepository<Device> deviceRepository,
        IDiscoveryService discoveryService,
        ILogger<TopologyService> logger)
    {
        _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TopologyMap> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTopology != null && (DateTime.UtcNow - _lastRebuild) < TimeSpan.FromMinutes(5))
        {
            return _cachedTopology;
        }

        return await RebuildTopologyAsync(cancellationToken);
    }

    public async Task<TopologyMap> GetTopologyForSubnetAsync(string subnetCidr, CancellationToken cancellationToken = default)
    {
        var fullTopology = await GetTopologyAsync(cancellationToken);

        // Filter nodes within subnet
        if (!IPNetwork.TryParse(subnetCidr, out var network))
        {
            return new TopologyMap(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Array.Empty<TopologyNode>(),
                Array.Empty<TopologyLink>(),
                new TopologyMetadata(0, 0, 0, 0, 0, 0, 0)
            );
        }

        var ipsInSubnet = network!.EnumerateIPAddresses().ToHashSet();

        var filteredNodes = fullTopology.Nodes
            .Where(n => ipsInSubnet.Contains(IPAddress.Parse(n.IpAddress)))
            .ToList();

        var filteredLinks = fullTopology.Links
            .Where(l => filteredNodes.Any(n => n.DeviceId == l.SourceNodeId) &&
                       filteredNodes.Any(n => n.DeviceId == l.TargetNodeId))
            .ToList();

        var metadata = new TopologyMetadata(
            TotalDevices: filteredNodes.Count,
            OnlineDevices: filteredNodes.Count(n => n.Status == DeviceStatus.Up),
            OfflineDevices: filteredNodes.Count(n => n.Status != DeviceStatus.Up),
            TotalLinks: filteredLinks.Count,
            ConfirmedLinks: filteredLinks.Count(l => l.Type is LinkType.Lldp or LinkType.Cdp),
            InferredLinks: filteredLinks.Count(l => l.Type == LinkType.Inferred),
            AverageConfidence: filteredLinks.Any() ? filteredLinks.Average(l => l.Confidence) : 0
        );

        return new TopologyMap(
            Guid.NewGuid(),
            DateTime.UtcNow,
            filteredNodes,
            filteredLinks,
            metadata
        );
    }

    public async Task<TopologyNode?> GetNodeAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var topology = await GetTopologyAsync(cancellationToken);
        return topology.Nodes.FirstOrDefault(n => n.DeviceId == deviceId);
    }

    public async Task<IReadOnlyList<TopologyLink>> GetLinksAsync(CancellationToken cancellationToken = default)
    {
        var topology = await GetTopologyAsync(cancellationToken);
        return topology.Links;
    }

    public async Task<TopologyPath?> FindPathAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var topology = await GetTopologyAsync(cancellationToken);

        var nodeMap = topology.Nodes.ToDictionary(n => n.DeviceId);
        var adjacency = new Dictionary<Guid, List<(Guid neighbor, Guid linkId, double cost)>>();

        foreach (var node in topology.Nodes)
        {
            adjacency[node.DeviceId] = new List<(Guid, Guid, double)>();
        }

        foreach (var link in topology.Links)
        {
            if (!adjacency.ContainsKey(link.SourceNodeId))
                adjacency[link.SourceNodeId] = new List<(Guid, Guid, double)>();

            var cost = link.Type switch
            {
                LinkType.Lldp => 1.0,
                LinkType.Cdp => 1.0,
                LinkType.Arp => 2.0,
                LinkType.MacTable => 2.0,
                LinkType.Manual => 1.0,
                LinkType.Inferred => 3.0,
                _ => 5.0
            };

            adjacency[link.SourceNodeId].Add((link.TargetNodeId, link.Id, cost));

            // Bidirectional
            if (!adjacency.ContainsKey(link.TargetNodeId))
                adjacency[link.TargetNodeId] = new List<(Guid, Guid, double)>();
            adjacency[link.TargetNodeId].Add((link.SourceNodeId, link.Id, cost));
        }

        // Dijkstra's algorithm
        var distances = new Dictionary<Guid, double>();
        var previous = new Dictionary<Guid, (Guid node, Guid link)>();
        var unvisited = new HashSet<Guid>(nodeMap.Keys);

        foreach (var nodeId in nodeMap.Keys)
        {
            distances[nodeId] = double.PositiveInfinity;
        }

        distances[sourceId] = 0;

        while (unvisited.Count > 0)
        {
            var current = unvisited.OrderBy(n => distances[n]).FirstOrDefault();
            if (current == Guid.Empty || distances[current] == double.PositiveInfinity)
                break;

            if (current == targetId)
                break;

            unvisited.Remove(current);

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var (neighbor, linkId, cost) in neighbors)
            {
                if (!unvisited.Contains(neighbor)) continue;

                var alt = distances[current] + cost;
                if (alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previous[neighbor] = (current, linkId);
                }
            }
        }

        if (!previous.ContainsKey(targetId))
            return null;

        // Reconstruct path
        var pathNodes = new List<Guid>();
        var pathLinks = new List<Guid>();
        var currentNode = targetId;

        while (currentNode != sourceId)
        {
            pathNodes.Insert(0, currentNode);
            if (previous.TryGetValue(currentNode, out var prev))
            {
                pathLinks.Insert(0, prev.link);
                currentNode = prev.node;
            }
            else
            {
                return null;
            }
        }
        pathNodes.Insert(0, sourceId);

        var totalCost = distances[targetId];
        return new TopologyPath(pathNodes, pathLinks, totalCost, true);
    }

    public async Task<TopologyMap> RebuildTopologyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rebuilding topology map...");

        var devices = await _deviceRepository.GetAllAsync(cancellationToken);
        var activeDevices = devices.Where(d => d.IsActive)
            .OrderBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var discoveredDevices = await _discoveryService.GetDiscoveredDevicesAsync(cancellationToken);
        var discoveredByIp = discoveredDevices
            .GroupBy(device => device.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.DiscoveredAt).First(),
                StringComparer.OrdinalIgnoreCase);

        var nodes = new List<TopologyNode>();
        var links = new List<TopologyLink>();

        for (var index = 0; index < activeDevices.Count; index++)
        {
            var device = activeDevices[index];
            discoveredByIp.TryGetValue(device.IpAddress, out var discovered);
            var angle = activeDevices.Count == 0 ? 0 : 2 * Math.PI * index / activeDevices.Count;
            var node = new TopologyNode(
                DeviceId: device.Id.Value,
                IpAddress: device.IpAddress,
                Hostname: device.Name,
                Vendor: discovered?.Vendor,
                DeviceType: device.Type,
                Status: device.Status,
                X: 500 + 350 * Math.Cos(angle),
                Y: 400 + 300 * Math.Sin(angle),
                Interfaces: discovered is null
                    ? Array.Empty<TopologyInterface>()
                    : discovered.Interfaces.Select(item => new TopologyInterface(
                        item.IfIndex, item.Name, item.MacAddress, item.AdminStatus, item.OperStatus, null)).ToList()
            );
            nodes.Add(node);
        }

        var nodesByIp = nodes.ToDictionary(node => node.IpAddress, StringComparer.OrdinalIgnoreCase);
        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceDiscovery in discoveredDevices)
        {
            if (!nodesByIp.TryGetValue(sourceDiscovery.IpAddress, out var sourceNode)) continue;
            foreach (var neighbor in sourceDiscovery.Neighbors)
            {
                var candidates = ResolveNeighborCandidates(neighbor, nodes, discoveredDevices)
                    .Where(candidate => candidate.DeviceId != sourceNode.DeviceId)
                    .DistinctBy(candidate => candidate.DeviceId)
                    .ToList();
                if (candidates.Count != 1) continue;

                var targetNode = candidates[0];
                var first = sourceNode.DeviceId.CompareTo(targetNode.DeviceId) < 0
                    ? sourceNode.DeviceId : targetNode.DeviceId;
                var second = first == sourceNode.DeviceId ? targetNode.DeviceId : sourceNode.DeviceId;
                var key = $"{first:N}|{second:N}|{neighbor.Protocol}|{neighbor.LocalInterface}|{neighbor.RemotePortId}";
                if (!seenLinks.Add(key)) continue;

                links.Add(new TopologyLink(
                    DeterministicGuid(key),
                    sourceNode.DeviceId,
                    targetNode.DeviceId,
                    neighbor.LocalInterface,
                    neighbor.RemotePortId,
                    neighbor.Protocol switch
                    {
                        NeighborProtocol.Lldp => LinkType.Lldp,
                        NeighborProtocol.Cdp => LinkType.Cdp,
                        NeighborProtocol.Arp => LinkType.Arp,
                        NeighborProtocol.MacTable => LinkType.MacTable,
                        _ => LinkType.Inferred
                    },
                    Math.Clamp(neighbor.Confidence, 0, 1),
                    sourceNode.Status == DeviceStatus.Up && targetNode.Status == DeviceStatus.Up
                        ? LinkStatus.Up : LinkStatus.Unknown));
            }
        }

        var metadata = new TopologyMetadata(
            TotalDevices: nodes.Count,
            OnlineDevices: nodes.Count(n => n.Status == DeviceStatus.Up),
            OfflineDevices: nodes.Count(n => n.Status != DeviceStatus.Up),
            TotalLinks: links.Count,
            ConfirmedLinks: links.Count(l => l.Type is LinkType.Lldp or LinkType.Cdp),
            InferredLinks: links.Count(l => l.Type == LinkType.Inferred),
            AverageConfidence: links.Any() ? links.Average(l => l.Confidence) : 0
        );

        _cachedTopology = new TopologyMap(
            Guid.NewGuid(),
            DateTime.UtcNow,
            nodes,
            links,
            metadata
        );

        _lastRebuild = DateTime.UtcNow;
        _logger.LogInformation("Topology rebuilt. Devices: {Count}, Links: {Links}", nodes.Count, links.Count);

        return _cachedTopology;
    }

    private static IEnumerable<TopologyNode> ResolveNeighborCandidates(
        DiscoveredNeighbor neighbor,
        IReadOnlyList<TopologyNode> nodes,
        IReadOnlyList<DiscoveredDevice> discoveredDevices)
    {
        if (IPAddress.TryParse(neighbor.RemoteChassisId, out var remoteIp))
        {
            foreach (var node in nodes.Where(node => node.IpAddress == remoteIp.ToString())) yield return node;
        }

        if (!string.IsNullOrWhiteSpace(neighbor.RemoteSystemName))
        {
            foreach (var node in nodes.Where(node =>
                         string.Equals(node.Hostname, neighbor.RemoteSystemName, StringComparison.OrdinalIgnoreCase)))
                yield return node;
        }

        var chassis = NormalizeHardwareAddress(neighbor.RemoteChassisId);
        if (chassis.Length == 12)
        {
            var matchingIps = discoveredDevices
                .Where(device => device.Interfaces.Any(item =>
                    NormalizeHardwareAddress(item.MacAddress) == chassis))
                .Select(device => device.IpAddress)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes.Where(node => matchingIps.Contains(node.IpAddress))) yield return node;
        }
    }

    private static string NormalizeHardwareAddress(string? value) =>
        new((value ?? string.Empty).Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
