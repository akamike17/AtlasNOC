using System.Net;
using System.Net.Sockets;
using System.Text;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Consulta SSDP M-SEARCH sin credenciales ni mutaciones.</summary>
public sealed class SsdpPresenceProbe : ILanPresenceProbe
{
    private static readonly IPEndPoint Multicast = new(IPAddress.Parse("239.255.255.250"), 1900);
    private const string Request = "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: ssdp:all\r\n\r\n";

    public async Task<IReadOnlySet<string>> DiscoverAsync(IReadOnlySet<string> allowedTargets, int timeoutMs,
        CancellationToken ct = default)
    {
        if (allowedTargets.Count == 0 || timeoutMs < 1) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);
        var probeToken = timeoutCts.Token;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ReceiveTimeout = Math.Max(1, timeoutMs)
        };
        socket.EnableBroadcast = false;
        var bytes = Encoding.ASCII.GetBytes(Request);
        await socket.SendToAsync(bytes, SocketFlags.None, Multicast, probeToken);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[8192];
        while (!probeToken.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), probeToken);
                if (result.RemoteEndPoint is IPEndPoint endpoint && allowedTargets.Contains(endpoint.Address.ToString()))
                    found.Add(endpoint.Address.ToString());
            }
            catch (SocketException) { break; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { break; }
        }
        return found;
    }
}
