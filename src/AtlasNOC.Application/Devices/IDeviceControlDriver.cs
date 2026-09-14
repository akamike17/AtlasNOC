namespace AtlasNOC.Application.Devices;

public enum DeviceAction { Reboot, EnableInterface, DisableInterface, SetInterfaceDescription }
public sealed record DeviceControlCapabilities(IReadOnlySet<DeviceAction> SupportedActions);
public sealed record DeviceActionRequest(string ManagementIp, DeviceAction Action, string? InterfaceName = null, string? Description = null);
public sealed record DeviceActionResult(bool Succeeded, string Message, string? Evidence = null);

/// Mutaciones explícitas y separadas del driver de lectura.
public interface IDeviceControlDriver
{
    string DriverKey { get; }
    Task<DeviceControlCapabilities> GetControlCapabilitiesAsync(CancellationToken ct = default);
    Task<DeviceActionResult> ExecuteAsync(DeviceActionRequest request, Services.ResolvedDeviceCredential credential, CancellationToken ct = default);
}
