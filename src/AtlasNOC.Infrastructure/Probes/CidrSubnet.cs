using System.Net;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Representación y enumeración de una subred CIDR (IPv4).</summary>
public sealed class CidrSubnet
{
    public IPAddress Network { get; }
    public int PrefixLength { get; }
    public IPAddress Broadcast { get; }

    private CidrSubnet(IPAddress network, int prefixLength, IPAddress broadcast)
    {
        Network = network;
        PrefixLength = prefixLength;
        Broadcast = broadcast;
    }

    public static bool TryParse(string value, out CidrSubnet result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var baseIp)) return false;
        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return false;

        var baseBytes = baseIp.GetAddressBytes();
        if (baseBytes.Length != 4) return false; // solo IPv4

        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var maskBytes = UintToBytes(mask);

        var netBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            netBytes[i] = (byte)(baseBytes[i] & maskBytes[i]);

        var netIp = new IPAddress(netBytes);

        var hostMask = prefix == 0 ? uint.MaxValue : ~mask;
        var hostMaskBytes = UintToBytes(hostMask);
        var broadcastBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            broadcastBytes[i] = (byte)(netBytes[i] | hostMaskBytes[i]);

        result = new CidrSubnet(netIp, prefix, new IPAddress(broadcastBytes));
        return true;
    }

    /// <summary>Enumerar hosts utilizables (excluye network y broadcast).</summary>
    public IReadOnlyList<IPAddress> ListIPAddress()
    {
        var start = ToUint(Network.GetAddressBytes());
        var end = ToUint(Broadcast.GetAddressBytes());

        if (PrefixLength == 32)
            return new[] { Network };
        if (end - start > 4096)
            return Array.Empty<IPAddress>(); // evitar enumerar redes gigantes

        var result = new List<IPAddress>();
        for (var current = start + 1; current < end; current++)
            result.Add(new IPAddress(UintToBytes(current)));
        return result;
    }

    private static uint ToUint(byte[] bytes)
    {
        var copy = (byte[])bytes.Clone();
        if (BitConverter.IsLittleEndian) Array.Reverse(copy);
        return BitConverter.ToUInt32(copy, 0);
    }

    private static byte[] UintToBytes(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }
}