using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

public class DeviceDriverRegistry : IDeviceDriverRegistry
{
    private readonly IReadOnlyList<IDeviceDriver> _drivers;

    public DeviceDriverRegistry(IEnumerable<IDeviceDriver> drivers)
    {
        _drivers = drivers.ToList();
    }

    public IDeviceDriver Resolve(DeviceFingerprint fingerprint)
    {
        foreach (var driver in _drivers)
            if (driver.CanHandle(fingerprint))
                return driver;
        throw new InvalidOperationException($"No driver found for {fingerprint.ManagementIp}");
    }

    public IReadOnlyList<IDeviceDriver> All() => _drivers;
}