using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public sealed class NetworkActionService : INetworkActionService
{
    private readonly AtlasNOCDbContext _db;
    private readonly IEnumerable<IDeviceControlDriver> _drivers;
    private readonly ICredentialService _credentials;
    private readonly IAuditService _audit;
    public NetworkActionService(AtlasNOCDbContext db, IEnumerable<IDeviceControlDriver> drivers, ICredentialService credentials, IAuditService audit)
    { _db = db; _drivers = drivers; _credentials = credentials; _audit = audit; }

    public async Task<NetworkActionPreview> PreviewAsync(Guid deviceId, DeviceAction action, string? interfaceName,
        CancellationToken ct = default)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id.Value == deviceId, ct);
        if (device is null)
            return new(deviceId, action, false, "DEVICE_MISSING", NetworkActionRisk.High,
                new(0, 0, 0, false, "Impacto no determinable: dispositivo inexistente."), true, false, "UNKNOWN",
                new[] { "El dispositivo no existe." });
        var driver = _drivers.FirstOrDefault(x => x.DriverKey.Equals(device.DriverKey, StringComparison.OrdinalIgnoreCase));
        var supported = driver is not null && (await driver.GetControlCapabilitiesAsync(ct)).SupportedActions.Contains(action);
        var candidates = await _db.DeviceCredentials.AsNoTracking()
            .Where(x => x.CanUse && x.DeviceId != null && x.DeviceId.Value == deviceId
                && (x.DriverKey == null || x.DriverKey == device.DriverKey))
            .ToListAsync(ct);
        var preferred = candidates.Count(x => x.IsPreferred);
        var credentialState = candidates.Count == 0 ? "CREDENTIAL_MISSING" : candidates.Count > 1 && preferred != 1 ? "CREDENTIAL_AMBIGUOUS" : "READY";
        var impact = new NetworkImpactResult(0, 0, 0, false, "Impacto parcial: la topología de este camino está incompleta.");
        var risk = action == DeviceAction.Reboot || action is DeviceAction.EnableInterface or DeviceAction.DisableInterface
            ? NetworkActionRisk.High : NetworkActionRisk.Medium;
        var warnings = new List<string>();
        if (driver is null) warnings.Add("No existe driver de control probado.");
        if (!supported) warnings.Add("La acción no está soportada por el driver.");
        if (credentialState != "READY") warnings.Add(credentialState);
        if (string.IsNullOrWhiteSpace(interfaceName) && action != DeviceAction.Reboot) warnings.Add("La interfaz es obligatoria.");
        return new(deviceId, action, supported, credentialState, risk, impact, risk >= NetworkActionRisk.Medium,
            action != DeviceAction.SetInterfaceDescription, device.IsManaged ? "MANAGEABLE" : "UNKNOWN", warnings);
    }

    public async Task<NetworkActionResult> ExecuteAsync(Guid deviceId, DeviceAction action, string? interfaceName, string? description, string actor, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(x => x.Id.Value == deviceId, ct);
        if (device is null) return new(false, "Dispositivo no encontrado.", null);
        var driver = _drivers.FirstOrDefault(x => x.DriverKey.Equals(device.DriverKey, StringComparison.OrdinalIgnoreCase));
        if (driver is null) return new(false, "El dispositivo no tiene driver de control probado.", null);
        var capabilities = await driver.GetControlCapabilitiesAsync(ct);
        if (!capabilities.SupportedActions.Contains(action)) return new(false, "Acción no soportada por el dispositivo.", null);
        var candidates = await _db.DeviceCredentials.AsNoTracking()
            .Where(x => x.CanUse && x.DeviceId != null && x.DeviceId.Value == deviceId
                && (x.DriverKey == null || x.DriverKey == device.DriverKey)
                && (x.SiteId == null || device.SiteId == null || x.SiteId.Value == device.SiteId.Value))
            .OrderByDescending(x => x.IsPreferred)
            .Select(x => x.Id.Value)
            .ToListAsync(ct);
        if (!candidates.Any()) return new(false, "CREDENTIAL_MISSING", null);
        var preferredCount = await _db.DeviceCredentials.AsNoTracking()
            .Where(x => candidates.Contains(x.Id.Value) && x.IsPreferred)
            .CountAsync(ct);
        if (candidates.Count() > 1 && preferredCount != 1)
            return new(false, "CREDENTIAL_AMBIGUOUS", null);
        var credentialId = candidates[0];
        var credential = await _credentials.ResolveAsync(credentialId, ct);
        if (credential is null) return new(false, "No se pudo resolver la credencial.", null);
        var result = await driver.ExecuteAsync(new DeviceActionRequest(device.ManagementIp, action, interfaceName, description), credential, ct);
        await _audit.RecordAsync("NetworkAction", action.ToString(), actor, actor, "Operator", deviceId.ToString(), "Device", ct);
        return new(result.Succeeded, result.Message, result.Evidence);
    }
}
