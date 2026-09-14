using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public sealed class ManualPaymentProvider : IPaymentProvider
{
    public string ProviderKey => "manual";
    public Task<PaymentResult> CaptureAsync(PaymentRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentResult(request.Amount > 0, request.Reference, request.Amount > 0 ? "Pago manual registrado para conciliación." : "El monto debe ser mayor a cero.", request.Amount > 0 ? PaymentCaptureState.Accepted : PaymentCaptureState.Rejected));
}

public sealed class WispOperationsService : IWispOperationsService
{
    private readonly IOperationsSnapshotService _snapshot;
    public WispOperationsService(IOperationsSnapshotService snapshot) => _snapshot = snapshot;
    public async Task<IReadOnlyList<OperationsDeviceDto>> ListOperationalClientsAsync(CancellationToken ct = default)
        => (await _snapshot.GetAsync(ct)).DeviceStates;
}

public sealed class TechnicianRoutePlanner : ITechnicianRoutePlanner
{
    private readonly AtlasNOCDbContext _db;
    public TechnicianRoutePlanner(AtlasNOCDbContext db) => _db = db;
    public async Task<IReadOnlyList<TechnicianRouteStop>> PlanAsync(IReadOnlyList<Guid> visitIds, CancellationToken ct = default)
    {
        var visits = await _db.TechnicianVisits.AsNoTracking().Where(x => visitIds.Contains(x.Id)).OrderBy(x => x.ScheduledAtUtc).ToListAsync(ct);
        var eta = DateTime.UtcNow;
        return visits.Select(v => { eta = eta < v.ScheduledAtUtc ? v.ScheduledAtUtc : eta; var duration = v.ActualMinutes ?? v.EstimatedMinutes; var travel = v.TravelMinutes ?? 0; var result = new TechnicianRouteStop(v.Id, eta, duration); eta = eta.AddMinutes((duration + travel) * (v.ActualMinutes.HasValue ? 1.0 : 1.15)); return result; }).ToList();
    }
}