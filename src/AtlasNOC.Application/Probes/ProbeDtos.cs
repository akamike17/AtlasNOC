namespace AtlasNOC.Application.Probes;

/// <summary>Fingerprint neutro que identifica un dispositivo para seleccionar un driver.</summary>
public sealed record DeviceFingerprint(
    string ManagementIp,
    string? SysName,
    string? SysObjectId,
    string? SysDescription,
    string? VendorHint);

/// <summary>Identidad neutral de un dispositivo adquirida por un adaptador.</summary>
public sealed record DeviceIdentity(
    string Hostname,
    string? Model,
    string? SerialNumber,
    string? FirmwareVersion,
    string? SysObjectId);

/// <summary>Interfaz neutral adquirida por un adaptador.</summary>
public sealed record InterfaceData(
    int IfIndex,
    string Name,
    string? Description,
    string? MacAddress,
    string? IpAddress,
    int AdminStatus,
    int OperStatus,
    ulong? SpeedBps,
    string? InterfaceType);

/// <summary>Vecino observado (evidencia cruda) adquirido por un adaptador.</summary>
public sealed record NeighborData(
    string RemoteIdentity,
    string? RemotePortIdentity,
    string LocalInterfaceName,
    string Protocol,
    string RawEvidenceHash);

/// <summary>Conjunto de métricas neutras de salud de un dispositivo.</summary>
public sealed record HealthData(
    double? LatencyMs,
    double? AvailabilityPercent,
    double? CpuPercent,
    double? MemoryPercent,
    long? UptimeSeconds);

/// <summary>Métrica puntual neutral (nombre, valor, unidad).</summary>
public sealed record MetricDatum(string Name, double Value, string? Unit = null);

/// <summary>Asociación inalámbrica neutral.</summary>
public sealed record WirelessClientData(
    string CpeMacAddress,
    string? CpeName,
    double? SignalDbm,
    double? NoiseDbm,
    double? Snr,
    double? TxRateMbps,
    double? RxRateMbps,
    string? SectorName);