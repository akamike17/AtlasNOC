using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Services;

public sealed class LocalNetworkProfileService : ILocalNetworkProfileService
{
    public IReadOnlyList<LocalNetworkProfileDto> GetProfiles()
    {
        var result = new List<LocalNetworkProfileDto>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || network.OperationalStatus != OperationalStatus.Up)
                continue;

            var properties = network.GetIPProperties();
            var gateway = properties.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

            foreach (var address in properties.UnicastAddresses
                         .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                     && !addressIsLinkLocal(a.Address)))
            {
                var mask = address.IPv4Mask;
                if (mask is null) continue;
                var scope = $"{Network(address.Address, mask)}/{PrefixLength(mask)}";
                result.Add(new LocalNetworkProfileDto(
                    network.Name, address.Address.ToString(), scope,
                    gateway?.ToString(), true));
            }
        }
        return result
            .OrderByDescending(p => !string.IsNullOrWhiteSpace(p.Gateway))
            .ThenBy(p => p.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool addressIsLinkLocal(IPAddress address)
        => address.GetAddressBytes() is [169, 254, _, _];

    private static IPAddress Network(IPAddress address, IPAddress mask)
    {
        var ip = address.GetAddressBytes();
        var bits = mask.GetAddressBytes();
        for (var i = 0; i < ip.Length; i++) ip[i] &= bits[i];
        return new IPAddress(ip);
    }

    private static int PrefixLength(IPAddress mask)
        => mask.GetAddressBytes().Sum(b => Convert.ToString(b, 2).Count(c => c == '1'));
}
