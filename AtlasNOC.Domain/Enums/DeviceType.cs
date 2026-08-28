using System;

namespace AtlasNOC.Domain.Enums;

/// <summary>
/// Tipo de dispositivo de red — valores alineados con SNMP sysObjectID y usados por el modelo de dominio.
/// </summary>
public enum DeviceType
{
    Unknown = 0,
    Router = 1,
    Switch = 2,
    Firewall = 3,
    Server = 4,
    AccessPoint = 5,
    Printer = 6,
    IoT = 7,
    Other = 99
}
