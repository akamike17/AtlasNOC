using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Devices;

/// RouterOS REST control. Sólo expone acciones implementadas en endpoints concretos.
public sealed class MikroTikControlDriver : IDeviceControlDriver
{
    private readonly IHttpClientFactory _http;
    public MikroTikControlDriver(IHttpClientFactory http) => _http = http;
    public string DriverKey => "mikrotik";
    public Task<DeviceControlCapabilities> GetControlCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceControlCapabilities(new HashSet<DeviceAction>
            { DeviceAction.Reboot, DeviceAction.EnableInterface, DeviceAction.DisableInterface, DeviceAction.SetInterfaceDescription }));

    public async Task<DeviceActionResult> ExecuteAsync(DeviceActionRequest request, ResolvedDeviceCredential credential, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Action)) return new(false, "Acción no soportada.");
        if (request.Action != DeviceAction.Reboot && string.IsNullOrWhiteSpace(request.InterfaceName))
            return new(false, "La interfaz es obligatoria para esta acción.");
        if (request.Action == DeviceAction.SetInterfaceDescription && request.Description is null)
            return new(false, "La descripción es obligatoria.");
        using var client = _http.CreateClient("mikrotik");
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint(request));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.UserName}:{credential.AuthPassword}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        if (request.Action != DeviceAction.Reboot)
            req.Content = JsonContent.Create(new Dictionary<string, string> { [".id"] = request.InterfaceName!,
                ["disabled"] = request.Action == DeviceAction.DisableInterface ? "true" : "false",
                ["comment"] = request.Description ?? string.Empty });
        using var response = await client.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return new(false, $"RouterOS devolvió HTTP {(int)response.StatusCode}.");
        return new(true, "Acción ejecutada y aceptada por RouterOS.", $"HTTP {(int)response.StatusCode} {request.Action}");
    }
    private static string Endpoint(DeviceActionRequest r) => r.Action == DeviceAction.Reboot
        ? $"https://{r.ManagementIp}/rest/system/reboot"
        : $"https://{r.ManagementIp}/rest/interface/set";
}
