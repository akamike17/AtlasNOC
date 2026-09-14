using System.Net;
using System.Net.Sockets;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Consulta mDNS de presencia, sin credenciales ni mutaciones.</summary>
public sealed class MdnsPresenceProbe : ILanPresenceProbe
{
    private static readonly IPEndPoint Multicast = new(IPAddress.Parse("224.0.0.251"), 5353);

    public async Task<IReadOnlySet<string>> DiscoverAsync(IReadOnlySet<string> allowedTargets, int timeoutMs,
        CancellationToken ct = default)
    {
        if (allowedTargets.Count == 0 || timeoutMs < 1) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 5353));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(Multicast.Address, IPAddress.Any));
        }
        catch (SocketException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        var query = BuildQuery();
        await socket.SendToAsync(query, SocketFlags.None, Multicast, timeoutCts.Token);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[9000];
        while (!timeoutCts.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None,
                    new IPEndPoint(IPAddress.Any, 0), timeoutCts.Token);
                if (result.RemoteEndPoint is IPEndPoint endpoint && allowedTargets.Contains(endpoint.Address.ToString()))
                    found.Add(endpoint.Address.ToString());
            }
            catch (SocketException) { break; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { break; }
        }
        return found;
    }

    private static byte[] BuildQuery()
    {
        // DNS query for PTR _services._dns-sd._udp.local, transaction ID 0.
        var name = new byte[] { 9, (byte)'_', (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'i', (byte)'c', (byte)'e', (byte)'s',
            7, (byte)'_', (byte)'d', (byte)'n', (byte)'s', (byte)'-', (byte)'s', (byte)'d', 4, (byte)'_', (byte)'u', (byte)'d', (byte)'p',
            5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0 };
        var packet = new byte[12 + name.Length + 4];
        Buffer.BlockCopy(name, 0, packet, 12, name.Length);
        packet[5] = 1; // QDCOUNT
        packet[12 + name.Length + 1] = 12; // QTYPE PTR
        packet[12 + name.Length + 3] = 1; // QCLASS IN
        return packet;
    }
}
