using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Services;

/// <summary>Ejecuta polling de dispositivos gestionados y escribe métricas.</summary>
public interface IPollingService
{
    Task PollAllManagedAsync(CancellationToken ct = default);
    Task PollDeviceAsync(Guid deviceId, CancellationToken ct = default);
}

/// <summary>Persiste muestras de métricas.</summary>
public interface IMetricWriter
{
    Task WriteAsync(IReadOnlyList<MetricSampleInput> samples, CancellationToken ct = default);
}

public sealed record MetricSampleInput(
    string ResourceType,
    string ResourceId,
    string MetricName,
    double Value,
    DateTime TimestampUtc,
    string? Unit = null,
    string? Quality = null);

/// <summary>Gestión de credenciales (las almacena cifradas).</summary>
public interface ICredentialService
{
    Task<Guid> CreateCredentialAsync(CreateCredentialRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CredentialDto>> ListCredentialsAsync(CancellationToken ct = default);
}

public sealed record CreateCredentialRequest(string Name, int SnmpVersion, string? UserName,
    string? AuthProtocol, string? PrivProtocol, string? Community, string? AuthPassword, string? PrivPassword);

public sealed record CredentialDto(Guid Id, string Name, int SnmpVersion, bool IsActive);

/// <summary>Registra eventos de auditoría.</summary>
public interface IAuditService
{
    Task RecordAsync(string category, string action, string actorUserId, string actorEmail,
        string actorRole, string? targetResource = null, string? targetResourceType = null,
        CancellationToken ct = default);
}