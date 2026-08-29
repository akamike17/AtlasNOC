using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>Identifica vendor/tipo a partir de sysObjectID/sysDescr/sysName (heurística declarativa).</summary>
public class NetworkFingerprintService : INetworkFingerprintService
{
    public string ResolveVendor(DeviceFingerprint fp)
    {
        var text = $"{fp.SysObjectId} {fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        if (text.Contains("mikrotik") || text.Contains("routeros") || text.Contains("1.3.6.1.4.1.14988"))
            return "mikrotik";
        if (text.Contains("ubiquiti") || text.Contains("unifi") || text.Contains("airmax") || text.Contains("1.3.6.1.4.1.41112"))
            return "ubiquiti";
        if (text.Contains("cisco") || text.Contains("1.3.6.1.4.1.9"))
            return "cisco";
        if (text.Contains("juniper") || text.Contains("1.3.6.1.4.1.2636"))
            return "juniper";
        if (text.Contains("hp") || text.Contains("aruba") || text.Contains("1.3.6.1.4.1.11"))
            return "hpe";
        return "generic";
    }

    public int ResolveDeviceType(DeviceFingerprint fp)
    {
        var text = $"{fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        if (text.Contains("access point") || text.Contains("ap") || text.Contains("unifi ap")) return 6;   // AccessPoint
        if (text.Contains("cpe") || text.Contains("station")) return 8;                                      // Cpe
        if (text.Contains("backhaul")) return 7;                                                             // Backhaul
        if (text.Contains("switch")) return 2;                                                               // Switch
        if (text.Contains("router")) return 1;                                                               // Router
        if (text.Contains("firewall")) return 3;                                                             // Firewall
        return 0; // Unknown
    }
}