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
        if (services.Count == 0) return new NetworkDiagnosticResult("NOT_FOUND", "Cliente sin servicios registrados.", Array.Empty<string>(), Array.Empty<Guid>());
        var serviceIds = services.Select(x => x.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var openAlerts = await _db.Alerts.AsNoTracking().Where(x => x.State != AtlasNOC.Domain.Enums.AlertState.Resolved && serviceIds.Contains(x.ResourceId)).ToListAsync(ct);
        var incidents = await _db.Incidents.AsNoTracking().Where(x => x.Status != AtlasNOC.Domain.Enums.IncidentStatus.Resolved && x.Status != AtlasNOC.Domain.Enums.IncidentStatus.Closed && x.RootCauseDeviceId != null).ToListAsync(ct);
        if (openAlerts.Count == 0 && incidents.Count == 0)
            return new NetworkDiagnosticResult("NotDetermined", "No existe evidencia vinculada al camino técnico del servicio; no se atribuye una falla.",
                new[] { $"Servicios registrados: {services.Count}", "No se usaron alertas globales no vinculadas." }, services.Select(x => x.Id).ToArray());
        var evidence = new[] { $"Servicios registrados: {services.Count}", $"Alertas vinculadas: {openAlerts.Count}", $"Incidentes con causa técnica: {incidents.Count}" };
        return new NetworkDiagnosticResult("Probable", "Existe evidencia técnica vinculada que requiere correlación del camino del servicio.", evidence, services.Select(x => x.Id).ToArray());
    }
}
