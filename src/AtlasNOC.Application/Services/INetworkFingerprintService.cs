using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Services;

/// <summary>Fingerprint de red: identifica vendor/tipo de un dispositivo a partir de sysObjectID/sysDescr.</summary>
public interface INetworkFingerprintService
{
    string ResolveVendor(DeviceFingerprint fingerprint);
    int ResolveDeviceType(DeviceFingerprint fingerprint);
}