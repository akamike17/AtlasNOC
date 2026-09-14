using System.Security.Cryptography;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Probe ICMP simple (disponibilidad + RTT). En Windows usa ping del SO.</summary>
public class IcmpProbe : IIcmpProbe
{
    private readonly ILogger<IcmpProbe> _logger;

    public IcmpProbe(ILogger<IcmpProbe> logger) => _logger = logger;

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
            {
                _logger.LogDebug("Discovery target {Target}: ping=success rttMs={RttMs}", ipAddress, reply.RoundtripTime);
                return new PingResult(true, (double)reply.RoundtripTime, null);
            }
            _logger.LogDebug("Discovery target {Target}: ping=failed status={Status}", ipAddress, reply.Status);
            return new PingResult(false, null, reply.Status.ToString());
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Discovery target {Target}: ping=cancelled; exception={ExceptionType}", ipAddress, ex.GetType().Name);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Discovery target {Target}: ping=timeout; exception={ExceptionType}", ipAddress, ex.GetType().Name);
            return new PingResult(false, null, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Discovery target {Target}: ping=error", ipAddress);
            return new PingResult(false, null, ex.Message);
        }
    }
}
