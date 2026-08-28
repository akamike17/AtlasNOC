using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface ISnmpService
{
    Task<SnmpResult> GetAsync(IPAddress ipAddress, Credential credential, string oid,
        TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<SnmpWalkResult> WalkAsync(IPAddress ipAddress, Credential credential, string oid,
        TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<SnmpSetResult> SetAsync(IPAddress ipAddress, Credential credential, string oid, string value,
        TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<SnmpTestResult> TestConnectionAsync(IPAddress ipAddress, Credential credential,
        TimeSpan timeout, CancellationToken cancellationToken = default);
}

public sealed record SnmpResult
(
    bool Success,
    string? Value,
    int ErrorStatus,
    int ErrorIndex,
    string? ErrorMessage,
    TimeSpan Elapsed
);

public sealed record SnmpWalkResult
(
    bool Success,
    IReadOnlyDictionary<string, string> Values,
    string? ErrorMessage,
    TimeSpan Elapsed
);

public sealed record SnmpSetResult
(
    bool Success,
    string? ErrorMessage,
    TimeSpan Elapsed
);

public sealed record SnmpTestResult
(
    bool Success,
    string? ErrorMessage,
    TimeSpan Elapsed
);
