using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de correlación topológica: consolida NeighborObservations no resueltas en NetworkLinks.</summary>
public class TopologyCorrelationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TopologyCorrelationWorker> _logger;

    public TopologyCorrelationWorker(IServiceScopeFactory scopeFactory, ILogger<TopologyCorrelationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
                var engine = scope.ServiceProvider.GetRequiredService<ITopologyCorrelationEngine>();

                var pending = await db.NeighborObservations
                    .Where(o => o.Status == NeighborObservationStatus.Pending
                        || o.Status == NeighborObservationStatus.Ambiguous)
                    .OrderBy(o => o.Id)
                    .Take(500)
                    .ToListAsync(stoppingToken);

                if (pending.Count > 0)
                {
                    var deviceIds = pending.Select(o => o.LocalDeviceId).Distinct().ToList();
                    var localDevices = await db.Devices.Where(d => deviceIds.Contains(d.Id)).ToListAsync(stoppingToken);
                    var allDevices = await db.Devices.AsNoTracking().ToListAsync(stoppingToken);
                    var identityCounts = allDevices
                        .GroupBy(d => Normalize(d.Hostname), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
                    var inputs = pending.Select(o => new NeighborObservationInput(
                        o.LocalDeviceId.Value.ToString(),
                        localDevices.FirstOrDefault(d => d.Id == o.LocalDeviceId)?.Hostname ?? string.Empty,
                        o.LocalInterfaceId.Value.ToString(),
                        o.RemoteIdentity,
                        o.RemotePortIdentity,
                        o.Protocol.ToString().ToLowerInvariant(),
                        o.RawEvidenceHash)).ToList();

                    var results = await engine.CorrelateAsync(inputs, stoppingToken);

                    foreach (var r in results)
                    {
                        var exists = await db.NetworkLinks.FirstOrDefaultAsync(l =>
                            (l.AInterfaceId.Value.ToString() == r.AInterfaceId && l.BInterfaceId.Value.ToString() == r.BInterfaceId)
                            || (l.AInterfaceId.Value.ToString() == r.BInterfaceId && l.BInterfaceId.Value.ToString() == r.AInterfaceId), stoppingToken);
                        if (exists is not null)
                        {
                            exists.RefreshEvidence((LinkType)r.LinkType, (DiscoverySource)r.DiscoverySource, r.Confidence);
                            continue;
                        }

                        db.NetworkLinks.Add(new NetworkLink(
                            InterfaceId.From(Guid.Parse(r.AInterfaceId)),
                            InterfaceId.From(Guid.Parse(r.BInterfaceId)),
                            (LinkType)r.LinkType,
                            (DiscoverySource)r.DiscoverySource,
                            r.Confidence));
                    }

                    var resolvedInterfaces = results
                        .SelectMany(r => new[] { r.AInterfaceId, r.BInterfaceId })
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var o in pending)
                    {
                        ClassifyObservation(o, identityCounts, resolvedInterfaces);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en TopologyCorrelationWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static string Normalize(string value) => value.Trim().TrimEnd('.').ToLowerInvariant();

    internal static void ClassifyObservation(NeighborObservation observation,
        IReadOnlyDictionary<string, int> identityCounts, IReadOnlySet<string> resolvedInterfaces)
    {
        if (string.IsNullOrWhiteSpace(observation.RemoteIdentity) || string.IsNullOrWhiteSpace(observation.RawEvidenceHash))
            observation.Reject();
        else if (identityCounts.GetValueOrDefault(Normalize(observation.RemoteIdentity)) > 1)
            observation.MarkAmbiguous();
        else if (resolvedInterfaces.Contains(observation.LocalInterfaceId.Value.ToString()))
            observation.Resolve();
        else
            observation.MarkPending();
    }
}
