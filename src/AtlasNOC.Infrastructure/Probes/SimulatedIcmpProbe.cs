using AtlasNOC.Application.Probes;
using AtlasNOC.Infrastructure.Devices;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>
/// Probe ICMP simulado para el modo LAB (§18). Responde "vivo" únicamente a las IPs
/// de la topología LAB-01; cualquier otra IP (fuera de 10.0.x) se considera no alcanzable.
/// Controlado explícitamente por el modo laboratorio, nunca usado en producción.
/// </summary>
public class SimulatedIcmpProbe : IIcmpProbe
{
    private readonly bool _enabled;
    private readonly IIcmpProbe _real;

    public SimulatedIcmpProbe(bool enabled, IIcmpProbe real)
    {
        _enabled = enabled;
        _real = real;
    }

    public Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct)
    {
        if (!_enabled)
            return _real.PingAsync(ipAddress, timeoutMs, ct);

        if (LabTopology.IsLabIp(ipAddress))
            return Task.FromResult(new PingResult(true, 1.0, null));

        // Fuera del rango LAB: no alcanzable (no se fabrica presencia).
        return Task.FromResult(new PingResult(false, null, "unreachable (lab)"));
    }
}