using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Devices;

/// Control explícitamente simulado; sólo se registra cuando LabMode=true.
public sealed class SimulatedDeviceControlDriver : IDeviceControlDriver
{
    private readonly Dictionary<string, bool> _interfaces = new(StringComparer.OrdinalIgnoreCase);
    public string DriverKey => "simulated";
    public Task<DeviceControlCapabilities> GetControlCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceControlCapabilities(Enum.GetValues<DeviceAction>().ToHashSet()));
    public Task<DeviceActionResult> ExecuteAsync(DeviceActionRequest request, ResolvedDeviceCredential credential, CancellationToken ct = default)
    {
        if (request.Action is DeviceAction.EnableInterface or DeviceAction.DisableInterface)
            _interfaces[$"{request.ManagementIp}:{request.InterfaceName}"] = request.Action == DeviceAction.EnableInterface;
        return Task.FromResult(new DeviceActionResult(true, "Acción aplicada al dispositivo simulado.", "SIMULATED"));
    }
}
