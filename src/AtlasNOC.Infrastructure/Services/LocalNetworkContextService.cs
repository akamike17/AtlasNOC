using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Services;

public sealed class LocalNetworkContextService : ILocalNetworkContextService
{
    public IReadOnlyList<LocalNetworkContext> GetContexts()
    {
        var result = new List<LocalNetworkContext>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var properties = adapter.GetIPProperties();
            var gateways = properties.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(IsIpv4);
            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork ||
                    !IsUsable(address.Address) || address.IPv4Mask is null)
                    continue;
                result.Add(new LocalNetworkContext(adapter.Name, address.Address.ToString(),
                    gateways?.ToString(), ToCidr(address.Address, address.IPv4Mask)));
            }
        }
        return result;
    }

    public LocalNetworkContext? GetPreferredContext()
        => GetContexts().OrderByDescending(x => x.GatewayIp is not null).FirstOrDefault();

    private static bool IsIpv4(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetwork;
    private static bool IsUsable(IPAddress ip) => IsIpv4(ip) && !IPAddress.IsLoopback(ip) &&
        !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Broadcast);

    private static string ToCidr(IPAddress ip, IPAddress mask)
    {
        var ipBytes = ip.GetAddressBytes(); var maskBytes = mask.GetAddressBytes();
        var network = new byte[4]; var bits = 0;
        for (var i = 0; i < 4; i++) { network[i] = (byte)(ipBytes[i] & maskBytes[i]); bits += BitOperations.PopCount(maskBytes[i]); }
        return $"{new IPAddress(network)}/{bits}";
    }
}
