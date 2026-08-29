using AtlasNOC.Application.Dtos;

namespace AtlasNOC.Application.Services;

public interface ISiteService
{
    Task<IReadOnlyList<SiteDto>> ListSitesAsync(CancellationToken ct = default);
    Task<SiteDto?> GetSiteAsync(Guid id, CancellationToken ct = default);
    Task<SiteDto> CreateSiteAsync(CreateSiteRequest request, CancellationToken ct = default);
}

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceDto>> ListDevicesAsync(CancellationToken ct = default);
    Task<DeviceDto?> GetDeviceAsync(Guid id, CancellationToken ct = default);
    Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken ct = default);
}

public interface ILinkService
{
    Task<IReadOnlyList<LinkDto>> ListLinksAsync(CancellationToken ct = default);
    Task ConfirmLinkAsync(Guid id, CancellationToken ct = default);
}

public interface ITopologyService
{
    Task<TopologyGraphDto> GetGraphAsync(TopologyFilter? filter, CancellationToken ct = default);
}

public interface IDiscoveryService
{
    Task<Guid> StartDiscoveryAsync(StartDiscoveryRequest request, CancellationToken ct = default);
    Task<DiscoveryRunDto?> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiscoveryRunDto>> ListRunsAsync(CancellationToken ct = default);
}

public interface IMetricQueryService
{
    Task<IReadOnlyList<MetricPointDto>> QueryAsync(string resourceType, string resourceId,
        string metricName, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface IAlertService
{
    Task<IReadOnlyList<AlertDto>> ListAlertsAsync(bool openOnly, CancellationToken ct = default);
    Task AcknowledgeAsync(Guid id, string by, CancellationToken ct = default);
    Task ResolveAsync(Guid id, string by, CancellationToken ct = default);
}

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentDto>> ListIncidentsAsync(bool activeOnly, CancellationToken ct = default);
    Task ResolveAsync(Guid id, string by, CancellationToken ct = default);
}

public interface IApiKeyService
{
    Task<ApiKeyCreateResultDto> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyLiteDto>> ListApiKeysAsync(CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
}

public interface ISystemHealthService
{
    Task<SystemHealthDto> GetHealthAsync(CancellationToken ct = default);
}