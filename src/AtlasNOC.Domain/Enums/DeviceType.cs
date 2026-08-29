namespace AtlasNOC.Domain.Enums;

/// <summary>Tipo de dispositivo de red, alineado con SNMP sysObjectID y drivers.</summary>
public enum DeviceType
{
    Unknown = 0,
    Router = 1,
    Switch = 2,
    Firewall = 3,
    Core = 4,
    Distribution = 5,
    AccessPoint = 6,
    Backhaul = 7,
    Cpe = 8,
    Server = 9,
    Other = 99
}