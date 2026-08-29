using System.Security.Cryptography;
using System.Text;
using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public class MetricQueryService : IMetricQueryService
{
    private readonly IMetricRepository _metrics;

    public MetricQueryService(IMetricRepository metrics) => _metrics = metrics;

    public async Task<IReadOnlyList<MetricPointDto>> QueryAsync(string resourceType, string resourceId,
        string metricName, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var samples = await _metrics.QueryAsync(resourceType, resourceId, metricName, fromUtc, toUtc, ct);
        return samples.Select(s => new MetricPointDto(s.TimestampUtc, s.ValueDouble, s.Unit)).ToList();
    }
}

public class AlertService : IAlertService
{
    private readonly IAlertRepository _alerts;
    private readonly AtlasNOCDbContext _context;

    public AlertService(IAlertRepository alerts, AtlasNOCDbContext context)
    {
        _alerts = alerts;
        _context = context;
    }

    public async Task<IReadOnlyList<AlertDto>> ListAlertsAsync(bool openOnly, CancellationToken ct = default)
    {
        var alerts = openOnly ? await _alerts.ListOpenAsync(ct) : await _alerts.ListAsync(ct);
        return alerts.Select(ToDto).ToList();
    }

    public async Task AcknowledgeAsync(Guid id, string by, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null) return;
        alert.Acknowledge(by);
        await _alerts.UpdateAsync(alert, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(Guid id, string by, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null) return;
        alert.Resolve(by);
        await _alerts.UpdateAsync(alert, ct);
        await _context.SaveChangesAsync(ct);
    }

    private static AlertDto ToDto(Alert a) => new(a.Id.Value, a.ResourceType, a.ResourceId, a.MetricName,
        a.Value, a.Threshold, (int)a.Severity, (int)a.State, a.FirstSeenUtc, a.LastSeenUtc);
}

public class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _incidents;
    private readonly AtlasNOCDbContext _context;

    public IncidentService(IIncidentRepository incidents, AtlasNOCDbContext context)
    {
        _incidents = incidents;
        _context = context;
    }

    public async Task<IReadOnlyList<IncidentDto>> ListIncidentsAsync(bool activeOnly, CancellationToken ct = default)
    {
        var list = activeOnly ? await _incidents.ListActiveAsync(ct)
            : await _context.Incidents.AsNoTracking().ToListAsync(ct);
        return list.Select(i => new IncidentDto(i.Id, i.Title, (int)i.Status, i.IsRootCauseCandidate, i.CreatedAtUtc)).ToList();
    }

    public async Task ResolveAsync(Guid id, string by, CancellationToken ct = default)
    {
        var incident = await _incidents.GetByIdAsync(id, ct);
        if (incident is null) return;
        incident.Resolve(by);
        await _incidents.UpdateAsync(incident, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class ApiKeyService : IApiKeyService
{
    private static readonly string Prefix = "atn_";

    private readonly IApiKeyRepository _keys;
    private readonly AtlasNOCDbContext _context;

    public ApiKeyService(IApiKeyRepository keys, AtlasNOCDbContext context)
    {
        _keys = keys;
        _context = context;
    }

    public async Task<ApiKeyCreateResultDto> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var plainText = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = HashKey(plainText);

        var apiKey = ApiKey.Create(request.Name, request.OwnerUserId ?? "system", hash,
            Prefix, request.Description, request.Scopes, request.ExpiresAtUtc);

        await _keys.AddAsync(apiKey, ct);
        await _context.SaveChangesAsync(ct);

        return new ApiKeyCreateResultDto(apiKey.Id, plainText, apiKey.Name);
    }

    public async Task<IReadOnlyList<ApiKeyLiteDto>> ListApiKeysAsync(CancellationToken ct = default)
    {
        var keys = await _keys.ListAsync(ct);
        return keys.Select(k => new ApiKeyLiteDto(k.Id, k.Name, k.Description, k.Scopes,
            k.IsActive && !k.IsExpired, k.CreatedAtUtc, k.ExpiresAtUtc)).ToList();
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _keys.GetByIdAsync(id, ct);
        if (key is null) return;
        key.Revoke();
        await _keys.UpdateAsync(key, ct);
        await _context.SaveChangesAsync(ct);
    }

    public static string HashKey(string plainText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class CredentialService : ICredentialService
{
    private readonly ICredentialRepository _credentials;
    private readonly ICredentialProtector _protector;
    private readonly AtlasNOCDbContext _context;

    public CredentialService(ICredentialRepository credentials, ICredentialProtector protector,
        AtlasNOCDbContext context)
    {
        _credentials = credentials;
        _protector = protector;
        _context = context;
    }

    public async Task<Guid> CreateCredentialAsync(CreateCredentialRequest request, CancellationToken ct = default)
    {
        var credential = new DeviceCredential(request.Name, (SnmpVersion)request.SnmpVersion,
            request.UserName, request.AuthProtocol, request.PrivProtocol);

        credential.SetProtectedSecrets(
            communityProtected: string.IsNullOrEmpty(request.Community) ? null : _protector.Protect(request.Community),
            authPasswordProtected: string.IsNullOrEmpty(request.AuthPassword) ? null : _protector.Protect(request.AuthPassword),
            privPasswordProtected: string.IsNullOrEmpty(request.PrivPassword) ? null : _protector.Protect(request.PrivPassword));

        await _credentials.AddAsync(credential, ct);
        await _context.SaveChangesAsync(ct);
        return credential.Id.Value;
    }

    public async Task<IReadOnlyList<CredentialDto>> ListCredentialsAsync(CancellationToken ct = default)
    {
        var list = await _credentials.ListAsync(ct);
        return list.Select(c => new CredentialDto(c.Id.Value, c.Name, (int)c.SnmpVersion, c.IsActive)).ToList();
    }
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _audits;
    private readonly AtlasNOCDbContext _context;

    public AuditService(IAuditRepository audits, AtlasNOCDbContext context)
    {
        _audits = audits;
        _context = context;
    }

    public async Task RecordAsync(string category, string action, string actorUserId, string actorEmail,
        string actorRole, string? targetResource = null, string? targetResourceType = null,
        CancellationToken ct = default)
    {
        var evt = new AuditEvent(category, action, actorUserId, actorEmail, actorRole,
            targetResource, targetResourceType);
        await _audits.AddAsync(evt, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class SystemHealthService : ISystemHealthService
{
    private readonly AtlasNOCDbContext _context;

    public SystemHealthService(AtlasNOCDbContext context) => _context = context;

    public async Task<SystemHealthDto> GetHealthAsync(CancellationToken ct = default)
    {
        bool dbOk;
        try { dbOk = await _context.Database.CanConnectAsync(ct); }
        catch { dbOk = false; }

        var deviceCount = await _context.Devices.CountAsync(ct);
        var openAlerts = await _context.Alerts.CountAsync(a => a.State != AlertState.Resolved, ct);

        return new SystemHealthDto(dbOk, deviceCount, openAlerts, DateTime.UtcNow);
    }
}