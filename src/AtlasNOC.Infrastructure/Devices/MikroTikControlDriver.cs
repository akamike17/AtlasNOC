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
        using var req = new HttpRequestMessage(request.Action == DeviceAction.Reboot ? HttpMethod.Post : HttpMethod.Patch, Endpoint(request));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.UserName}:{credential.AuthPassword}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        if (request.Action != DeviceAction.Reboot)
        {
            var body = request.Action switch
            {
                DeviceAction.EnableInterface => new Dictionary<string, string> { ["disabled"] = "false" },
                DeviceAction.DisableInterface => new Dictionary<string, string> { ["disabled"] = "true" },
                DeviceAction.SetInterfaceDescription => new Dictionary<string, string> { ["comment"] = request.Description! },
                _ => throw new ArgumentOutOfRangeException()
            };
            req.Content = JsonContent.Create(body);
        }
        using var response = await client.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return new(false, $"RouterOS devolvió HTTP {(int)response.StatusCode}.");
        return new(true, "Acción ejecutada y aceptada por RouterOS.", $"HTTP {(int)response.StatusCode} {request.Action}");
    }
    private static string Endpoint(DeviceActionRequest r) => r.Action == DeviceAction.Reboot
        ? $"https://{r.ManagementIp}/rest/system/reboot"
        : $"https://{r.ManagementIp}/rest/interface/{Uri.EscapeDataString(r.InterfaceName!)}";
}
