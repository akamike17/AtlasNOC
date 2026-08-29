using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Devices;

/// <summary>Registry que resuelve el driver adecuado para un fingerprint.</summary>
public interface IDeviceDriverRegistry
{
    IDeviceDriver Resolve(DeviceFingerprint fingerprint);
    IReadOnlyList<IDeviceDriver> All();
}