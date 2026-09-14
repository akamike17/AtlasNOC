using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public sealed class NetworkDiagnosticsService : INetworkDiagnosticService
{
    private readonly AtlasNOCDbContext _db;
    public NetworkDiagnosticsService(AtlasNOCDbContext db) => _db = db;
    public async Task<NetworkDiagnosticResult> DiagnoseCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var services = await _db.CustomerServices.AsNoTracking().Where(x => x.CustomerId == customerId).ToListAsync(ct);
        var openAlerts = await _db.Alerts.AsNoTracking().CountAsync(x => x.State != AtlasNOC.Domain.Enums.AlertState.Resolved, ct);
        var incidents = await _db.Incidents.AsNoTracking().CountAsync(x => x.Status != AtlasNOC.Domain.Enums.IncidentStatus.Resolved && x.Status != AtlasNOC.Domain.Enums.IncidentStatus.Closed, ct);
        if (services.Count == 0) return new NetworkDiagnosticResult("NOT_FOUND", "Cliente sin servicios registrados.", Array.Empty<string>(), Array.Empty<Guid>());
        var evidence = new[] { $"Servicios registrados: {services.Count}", $"Alertas abiertas: {openAlerts}", $"Incidentes abiertos: {incidents}" };
        var partial = openAlerts > 0 || incidents > 0;
        return new NetworkDiagnosticResult(partial ? "DEGRADED" : "NO_EVIDENCE_OF_FAILURE", partial ? "Hay evidencia operativa que requiere correlación adicional." : "No hay evidencia persistida de falla para este cliente.", evidence, services.Select(x => x.Id).ToArray());
    }
}
