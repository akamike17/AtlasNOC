using AtlasNOC.Application.Services;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Devices;

/// <summary>Driver que consume una credencial resuelta sólo durante una adquisición.</summary>
public interface IDeviceCredentialAwareDriver
{
    Task<DeviceIdentity> GetIdentityAsync(string managementIp, ResolvedDeviceCredential credential, CancellationToken ct);
    Task<IReadOnlyList<Probes.InterfaceData>> GetInterfacesAsync(string managementIp, ResolvedDeviceCredential credential, CancellationToken ct);
    Task<IReadOnlyList<Probes.NeighborData>> GetNeighborsAsync(string managementIp, ResolvedDeviceCredential credential, CancellationToken ct);
}
