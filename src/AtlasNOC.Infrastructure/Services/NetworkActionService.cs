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

    public async Task<NetworkActionResult> ExecuteAsync(Guid deviceId, DeviceAction action, string? interfaceName, string? description, string actor, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(x => x.Id.Value == deviceId, ct);
        if (device is null) return new(false, "Dispositivo no encontrado.", null);
        var driver = _drivers.FirstOrDefault(x => x.DriverKey.Equals(device.DriverKey, StringComparison.OrdinalIgnoreCase));
        if (driver is null) return new(false, "El dispositivo no tiene driver de control probado.", null);
        var capabilities = await driver.GetControlCapabilitiesAsync(ct);
        if (!capabilities.SupportedActions.Contains(action)) return new(false, "Acción no soportada por el dispositivo.", null);
        var credentialId = await _db.DeviceCredentials.AsNoTracking().Where(x => x.CanUse).Select(x => (Guid?)x.Id.Value).FirstOrDefaultAsync(ct);
        if (!credentialId.HasValue) return new(false, "No existe una credencial válida para ejecutar la acción.", null);
        var credential = await _credentials.ResolveAsync(credentialId.Value, ct);
        if (credential is null) return new(false, "No se pudo resolver la credencial.", null);
        var result = await driver.ExecuteAsync(new DeviceActionRequest(device.ManagementIp, action, interfaceName, description), credential, ct);
        await _audit.RecordAsync("NetworkAction", action.ToString(), actor, actor, "Operator", deviceId.ToString(), "Device", ct);
        return new(result.Succeeded, result.Message, result.Evidence);
    }
}
