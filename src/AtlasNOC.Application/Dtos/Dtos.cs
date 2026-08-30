namespace AtlasNOC.Application.Dtos;

public sealed record SetupRequest(
    string WispName,
    string AdminUserName,
    string AdminDisplayName,
    string Password,
    string ConfirmPassword);

public sealed record SetupResult(bool Success, string? ErrorMessage);

public sealed record UserLiteDto(Guid Id, string UserName, string? DisplayName, string Role, bool IsActive);

public sealed record SiteDto(Guid Id, string Name, string Code, int SiteType, int DeviceCount);

public sealed record CreateSiteRequest(string Name, string Code, int SiteType, double? Latitude, double? Longitude, string? Address);

public sealed record DeviceDto(Guid Id, string Hostname, string ManagementIp, int DeviceType, int Vendor,
    string? Model, int Status, DateTime? LastSeenAtUtc, Guid? SiteId, bool IsManaged);

public sealed record CreateDeviceRequest(string Hostname, string ManagementIp, int DeviceType, int Vendor,
    Guid? SiteId, string? Model);

public sealed record LinkDto(Guid Id, Guid AInterfaceId, Guid BInterfaceId, int LinkType, int DiscoverySource,
    double Confidence, bool IsConfirmed, bool IsStale, bool IsManual);

public sealed record TopologyFilter(Guid? SiteId, int? Status, int? DeviceType, int? Vendor, string? Query, bool HideCpe);

public sealed record TopologyNodeDto(Guid Id, string Label, string Ip, int DeviceType, int Vendor, int Status, Guid? SiteId);

public sealed record TopologyEdgeDto(Guid Id, Guid Source, Guid Target, int LinkType, int Status, bool IsConfirmed);

public sealed record TopologyGroupDto(Guid SiteId, string Label);

public sealed record TopologyGraphDto(
    IReadOnlyList<TopologyNodeDto> Nodes,
    IReadOnlyList<TopologyEdgeDto> Edges,
    IReadOnlyList<TopologyGroupDto> Groups,
    int UnlinkedNodeCount);

public sealed record StartDiscoveryRequest(string ScopeIp, Guid? SiteId, Guid? CredentialId);

public sealed record DiscoveryRunDto(Guid Id, string ScopeIp, int Status, DateTime StartedAtUtc,
    int FoundCount, int NewCount, int UpdatedCount, int ConfirmedLinkCount, int PendingRelationCount, int FailureCount);

public sealed record MetricPointDto(DateTime TimestampUtc, double Value, string? Unit);

public sealed record AlertDto(Guid Id, string ResourceType, string ResourceId, string MetricName,
    double Value, double Threshold, int Severity, int State, DateTime FirstSeenUtc, DateTime LastSeenUtc);

public sealed record IncidentDto(Guid Id, string Title, int Status, bool IsRootCauseCandidate, DateTime CreatedAtUtc);

public sealed record CreateApiKeyRequest(string Name, string Description, string Scopes, DateTime? ExpiresAtUtc, string? OwnerUserId = null);

public sealed record ApiKeyCreateResultDto(Guid Id, string PlainTextKey, string Name);

public sealed record ApiKeyLiteDto(Guid Id, string Name, string Description, string Scopes,
    bool IsActive, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc);

public sealed record SystemHealthDto(bool DatabaseOk, int DeviceCount, int OpenAlertCount, DateTime CheckedAtUtc);

public sealed record AlertRuleDto(Guid Id, string Name, string MetricName, string ComparisonOperator,
    double Threshold, int Severity, int ConsecutiveFaults, bool IsEnabled);

public sealed record CreateAlertRuleRequest(string Name, string MetricName, string ComparisonOperator,
    double Threshold, int Severity, int ConsecutiveFaults);