using System.Security.Cryptography;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Probe ICMP simple (disponibilidad + RTT). En Windows usa ping del SO.</summary>
public class IcmpProbe : IIcmpProbe
{
    public async Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return new PingResult(true, (double)reply.RoundtripTime, null);
            return new PingResult(false, null, reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new PingResult(false, null, ex.Message);
        }
    }
}