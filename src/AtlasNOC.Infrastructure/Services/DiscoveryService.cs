using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>
/// Descubrimiento: valida alcance, crea DiscoveryRun y dispara el trabajo en segundo plano.
/// La adquisición real (ICMP/SNMP/drivers) la ejecuta un worker/adaptador; aquí se gestiona el ciclo de vida.
/// </summary>
public class DiscoveryService : IDiscoveryService
{
    private readonly IDiscoveryRunRepository _runs;
    private readonly AtlasNOCDbContext _context;

    public DiscoveryService(IDiscoveryRunRepository runs, AtlasNOCDbContext context)
    {
        _runs = runs;
        _context = context;
    }

    public async Task<Guid> StartDiscoveryAsync(StartDiscoveryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ScopeIp))
            throw new ArgumentException("El alcance (CIDR/IPs) es obligatorio.", nameof(request.ScopeIp));

        var run = new DiscoveryRun(request.ScopeIp,
            request.SiteId?.ToString(), request.CredentialId?.ToString());
        run.Start();

        await _runs.AddAsync(run, ct);
        await _context.SaveChangesAsync(ct);

        return run.Id;
    }

    public async Task<DiscoveryRunDto?> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        return run is null ? null : ToDto(run);
    }

    public async Task<IReadOnlyList<DiscoveryRunDto>> ListRunsAsync(CancellationToken ct = default)
        => (await _context.DiscoveryRuns.AsNoTracking().OrderByDescending(r => r.StartedAtUtc).ToListAsync(ct))
            .Select(ToDto).ToList();

    private static DiscoveryRunDto ToDto(DiscoveryRun r) => new(
        r.Id, r.ScopeIp, (int)r.Status, r.StartedAtUtc, r.FoundCount, r.NewCount,
        r.UpdatedCount, r.ConfirmedLinkCount, r.PendingRelationCount, r.FailureCount);
}

public class MetricWriter : IMetricWriter
{
    private readonly IMetricRepository _metrics;
    private readonly AtlasNOCDbContext _context;

    public MetricWriter(IMetricRepository metrics, AtlasNOCDbContext context)
    {
        _metrics = metrics;
        _context = context;
    }

    public async Task WriteAsync(IReadOnlyList<MetricSampleInput> samples, CancellationToken ct = default)
    {
        if (samples is null || samples.Count == 0) return;
        var entities = samples.Select(s => new MetricSample(s.ResourceType, s.ResourceId,
            s.MetricName, s.Value, s.TimestampUtc, s.Unit, s.Quality)).ToList();
        await _metrics.AddSamplesAsync(entities, ct);
        await _context.SaveChangesAsync(ct);
    }
}