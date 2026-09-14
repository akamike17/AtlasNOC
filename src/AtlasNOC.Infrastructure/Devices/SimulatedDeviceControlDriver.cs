using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Devices;

/// Control explícitamente simulado; sólo se registra cuando LabMode=true.
public sealed class SimulatedDeviceControlDriver : IDeviceControlDriver
{
    public sealed record SimulatedInterfaceState(bool Enabled, string Description);
    private readonly Dictionary<string, SimulatedInterfaceState> _interfaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<DeviceAction> _unsupported = new();
    public bool FailNext { get; private set; }
    public bool TimedOut { get; private set; }
    public bool Rebooted { get; private set; }
    public string DriverKey => "simulated";
    public Task<DeviceControlCapabilities> GetControlCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceControlCapabilities(Enum.GetValues<DeviceAction>().Where(x => !_unsupported.Contains(x)).ToHashSet()));
    public void SetUnsupported(DeviceAction action) => _unsupported.Add(action);
    public void FailOnce() => FailNext = true;
    public void TimeoutNext() => TimedOut = true;
    public SimulatedInterfaceState? GetInterfaceState(string managementIp, string interfaceName)
        => _interfaces.GetValueOrDefault($"{managementIp}:{interfaceName}");
    public Task<DeviceActionResult> ExecuteAsync(DeviceActionRequest request, ResolvedDeviceCredential credential, CancellationToken ct = default)
    {
        if (_unsupported.Contains(request.Action)) return Task.FromResult(new DeviceActionResult(false, "UNSUPPORTED", "SIMULATED"));
        if (FailNext) { FailNext = false; return Task.FromResult(new DeviceActionResult(false, "SIMULATED_FAILURE", "SIMULATED")); }
        if (TimedOut) { TimedOut = false; return Task.FromCanceled<DeviceActionResult>(new CancellationToken(true)); }
        if (request.Action == DeviceAction.Reboot) { Rebooted = true; return Task.FromResult(new DeviceActionResult(true, "SIMULATED_REBOOT", "SIMULATED")); }
        var key = $"{request.ManagementIp}:{request.InterfaceName}";
        var current = _interfaces.GetValueOrDefault(key) ?? new SimulatedInterfaceState(true, string.Empty);
        if (request.Action is DeviceAction.EnableInterface or DeviceAction.DisableInterface)
            _interfaces[key] = current with { Enabled = request.Action == DeviceAction.EnableInterface };
        else if (request.Action == DeviceAction.SetInterfaceDescription)
            _interfaces[key] = current with { Description = request.Description ?? string.Empty };
        return Task.FromResult(new DeviceActionResult(true, "Acción aplicada al dispositivo simulado.", "SIMULATED"));
    }
}
