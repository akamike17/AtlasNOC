using System.Net;
using System.Runtime.InteropServices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Probes;

public sealed class ArpProbe : IArpProbe
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint phyAddrLen);

    public Task<bool> ResolveAsync(string ipAddress, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows() || !IPAddress.TryParse(ipAddress, out var address)
            || !IsEligibleTarget(address))
            return Task.FromResult(false);

        var bytes = address.GetAddressBytes();
        var destination = BitConverter.ToUInt32(bytes, 0);
        var mac = new byte[6];
        uint length = (uint)mac.Length;
        var result = SendARP(destination, 0, mac, ref length);
        return Task.FromResult(result == 0 && length > 0);
    }

    internal static bool IsEligibleTarget(IPAddress address)
        => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(address)
            && !address.Equals(IPAddress.Any)
            && !address.Equals(IPAddress.Broadcast)
            && address.GetAddressBytes()[0] is < 224 or > 239
            && !address.GetAddressBytes().SequenceEqual(new byte[] { 255, 255, 255, 255 });
}
