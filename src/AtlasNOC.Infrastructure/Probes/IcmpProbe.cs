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
            // Encadena el timeout del ping con el token de cancelación del ciclo.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs).WaitAsync(timeoutCts.Token);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return new PingResult(true, (double)reply.RoundtripTime, null);
            return new PingResult(false, null, reply.Status.ToString());
        }
        catch (OperationCanceledException)
        {
            return new PingResult(false, null, "timeout");
        }
        catch (Exception ex)
        {
            return new PingResult(false, null, ex.Message);
        }
    }
}