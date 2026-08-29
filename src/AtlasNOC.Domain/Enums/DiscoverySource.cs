namespace AtlasNOC.Domain.Enums;

public enum DiscoverySource
{
    Manual = 0,
    Lldp = 1,
    Cdp = 2,
    MikroTikNeighbor = 3,
    Ubiquiti = 4,
    WirelessAssociation = 5,
    Imported = 6
}