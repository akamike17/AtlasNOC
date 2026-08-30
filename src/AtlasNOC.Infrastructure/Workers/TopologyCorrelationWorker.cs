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
                    .Where(o => !o.IsResolved)
                    .Take(500)
                    .ToListAsync(stoppingToken);

                if (pending.Count > 0)
                {
                    var inputs = pending.Select(o => new NeighborObservationInput(
                        o.LocalDeviceId.Value.ToString(),
                        o.LocalInterfaceId.Value.ToString(),
                        o.RemoteIdentity,
                        o.RemotePortIdentity,
                        o.Protocol.ToString().ToLowerInvariant(),
                        o.RawEvidenceHash)).ToList();

                    var results = await engine.CorrelateAsync(inputs, stoppingToken);

                    foreach (var r in results)
                    {
                        var exists = await db.NetworkLinks.AnyAsync(l =>
                            (l.AInterfaceId.Value.ToString() == r.AInterfaceId && l.BInterfaceId.Value.ToString() == r.BInterfaceId)
                            || (l.AInterfaceId.Value.ToString() == r.BInterfaceId && l.BInterfaceId.Value.ToString() == r.AInterfaceId), stoppingToken);
                        if (exists) continue;

                        db.NetworkLinks.Add(new NetworkLink(
                            InterfaceId.From(Guid.Parse(r.AInterfaceId)),
                            InterfaceId.From(Guid.Parse(r.BInterfaceId)),
                            (LinkType)r.LinkType,
                            (DiscoverySource)r.DiscoverySource,
                            r.Confidence));
                    }

                    foreach (var o in pending) o.Resolve();
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en TopologyCorrelationWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}