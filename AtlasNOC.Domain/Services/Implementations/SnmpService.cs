using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public sealed class SnmpService : ISnmpService
{
    private const int SnmpPort = 161;
    private const int MaximumWalkVariables = 2048;
    private static int _requestId;
    private readonly bool _allowSet;
    private readonly ICredentialProtector _credentialProtector;

    public SnmpService(ICredentialProtector credentialProtector, bool allowSet = false)
    {
        _credentialProtector = credentialProtector ?? throw new ArgumentNullException(nameof(credentialProtector));
        _allowSet = allowSet;
    }

    public async Task<SnmpResult> GetAsync(IPAddress ipAddress, Credential credential, string oid,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var endpoint = CreateEndpoint(ipAddress, credential);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var (request, registry) = await CreateV3GetRequestAsync(
                endpoint, credential, new List<Variable> { new(ParseOid(oid)) }, timeoutCts.Token);
            var response = await request.GetResponseAsync(endpoint, registry, timeoutCts.Token);
            var pdu = response.Pdu();
            var errorStatus = pdu.ErrorStatus.ToInt32();
            var variable = pdu.Variables.FirstOrDefault();
            return new SnmpResult
            (
                Success: errorStatus == 0 && variable is not null,
                Value: variable?.Data.ToString(),
                ErrorStatus: errorStatus,
                ErrorIndex: pdu.ErrorIndex.ToInt32(),
                ErrorMessage: errorStatus == 0 ? null : pdu.ErrorStatus.ToString(),
                Elapsed: stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("SNMP request timed out.", -2, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or SnmpException or SocketException)
        {
            return Failure(exception.Message, -3, stopwatch.Elapsed);
        }
    }

    public async Task<SnmpWalkResult> WalkAsync(IPAddress ipAddress, Credential credential, string oid,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var endpoint = CreateEndpoint(ipAddress, credential);
            var root = ParseOid(oid);
            var rootText = root.ToString().TrimEnd('.');
            var current = root;
            var variables = new Dictionary<string, string>();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            while (variables.Count < MaximumWalkVariables)
            {
                var (request, registry) = await CreateV3GetNextRequestAsync(
                    endpoint, credential, new List<Variable> { new(current) }, timeoutCts.Token);
                var response = await request.GetResponseAsync(endpoint, registry, timeoutCts.Token);
                var pdu = response.Pdu();
                if (pdu.ErrorStatus.ToInt32() != 0) break;
                var next = pdu.Variables.FirstOrDefault();
                if (next is null) break;
                var nextOid = next.Id.ToString();
                if (nextOid == current.ToString() ||
                    !(nextOid == rootText || nextOid.StartsWith(rootText + ".", StringComparison.Ordinal))) break;

                variables[nextOid] = next.Data.ToString();
                current = next.Id;
            }

            return new SnmpWalkResult(true, variables, null, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SnmpWalkResult(false, new Dictionary<string, string>(), "SNMP walk timed out.", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or SnmpException or SocketException)
        {
            return new SnmpWalkResult(false, new Dictionary<string, string>(), exception.Message, stopwatch.Elapsed);
        }
    }

    public async Task<SnmpSetResult> SetAsync(IPAddress ipAddress, Credential credential, string oid, string value,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!_allowSet)
        {
            return new SnmpSetResult(false, "SNMP SET is disabled by default.", stopwatch.Elapsed);
        }

        try
        {
            var endpoint = CreateEndpoint(ipAddress, credential);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var (request, registry) = await CreateV3SetRequestAsync(endpoint, credential,
                new List<Variable> { new(ParseOid(oid), new OctetString(value)) }, timeoutCts.Token);
            var response = await request.GetResponseAsync(endpoint, registry, timeoutCts.Token);
            var pdu = response.Pdu();
            var errorStatus = pdu.ErrorStatus.ToInt32();

            return new SnmpSetResult(
                Success: errorStatus == 0,
                ErrorMessage: errorStatus == 0 ? null : pdu.ErrorStatus.ToString(),
                Elapsed: stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SnmpSetResult(false, "SNMP set timed out.", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or SnmpException or SocketException)
        {
            return new SnmpSetResult(false, exception.Message, stopwatch.Elapsed);
        }
    }

    public async Task<SnmpTestResult> TestConnectionAsync(IPAddress ipAddress, Credential credential,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!TestCredential(credential))
            {
                return new SnmpTestResult(false, "Invalid credential configuration", stopwatch.Elapsed);
            }

            var endpoint = CreateEndpoint(ipAddress, credential);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            // Test with sysDescr (1.3.6.1.2.1.1.1.0)
            var (request, registry) = await CreateV3GetRequestAsync(endpoint, credential,
                new List<Variable> { new(new ObjectIdentifier("1.3.6.1.2.1.1.1.0")) }, timeoutCts.Token);
            var response = await request.GetResponseAsync(endpoint, registry, timeoutCts.Token);
            var pdu = response.Pdu();
            var errorStatus = pdu.ErrorStatus.ToInt32();

            return new SnmpTestResult(
                Success: errorStatus == 0,
                ErrorMessage: errorStatus == 0 ? null : pdu.ErrorStatus.ToString(),
                Elapsed: stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SnmpTestResult(false, "SNMP test timed out.", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or SnmpException or SocketException)
        {
            return new SnmpTestResult(false, exception.Message, stopwatch.Elapsed);
        }
    }

    private static bool TestCredential(Credential credential)
    {
        if (credential.Version is SnmpVersion.V1 or SnmpVersion.V2c)
        {
            return !string.IsNullOrWhiteSpace(credential.Community);
        }

        return !string.IsNullOrWhiteSpace(credential.UserName) &&
            !string.IsNullOrWhiteSpace(credential.AuthProtocol) &&
            !string.IsNullOrWhiteSpace(credential.ProtectedAuthPassword) &&
            !string.IsNullOrWhiteSpace(credential.PrivProtocol) &&
            !string.IsNullOrWhiteSpace(credential.ProtectedPrivPassword);
    }

    private static IPEndPoint CreateEndpoint(IPAddress ipAddress, Credential credential)
    {
        return new IPEndPoint(ipAddress, SnmpPort);
    }

    private static VersionCode ToVersion(SnmpVersion version)
    {
        return version switch
        {
            SnmpVersion.V1 => VersionCode.V1,
            SnmpVersion.V2c => VersionCode.V2,
            SnmpVersion.V3 => VersionCode.V3,
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported SNMP version.")
        };
    }

    private static ObjectIdentifier ParseOid(string oid) => new ObjectIdentifier(oid);

    private string GetCommunity(Credential credential)
    {
        if (credential.Version is not (SnmpVersion.V1 or SnmpVersion.V2c) ||
            string.IsNullOrWhiteSpace(credential.Community))
        {
            throw new ArgumentException("A protected SNMP community is required.", nameof(credential));
        }

        return _credentialProtector.Unprotect(credential.Community);
    }

    private async Task<(ISnmpMessage Request, UserRegistry Registry)> CreateV3GetRequestAsync(
        IPEndPoint endpoint, Credential credential, IList<Variable> variables, CancellationToken token)
    {
        if (credential.Version != SnmpVersion.V3)
            return (new GetRequestMessage(NextRequestId(), ToVersion(credential.Version),
                new OctetString(GetCommunity(credential)), variables), new UserRegistry());

        var security = CreateV3Security(credential);
        var report = await Messenger.GetNextDiscovery(SnmpType.GetRequestPdu).GetResponseAsync(endpoint, token);
        return (new GetRequestMessage(VersionCode.V3, NextRequestId(), NextRequestId(), security.UserName,
            OctetString.Empty, variables, security.Privacy, 65535, report), security.Registry);
    }

    private async Task<(ISnmpMessage Request, UserRegistry Registry)> CreateV3GetNextRequestAsync(
        IPEndPoint endpoint, Credential credential, IList<Variable> variables, CancellationToken token)
    {
        if (credential.Version != SnmpVersion.V3)
            return (new GetNextRequestMessage(NextRequestId(), ToVersion(credential.Version),
                new OctetString(GetCommunity(credential)), variables), new UserRegistry());

        var security = CreateV3Security(credential);
        var report = await Messenger.GetNextDiscovery(SnmpType.GetNextRequestPdu).GetResponseAsync(endpoint, token);
        return (new GetNextRequestMessage(VersionCode.V3, NextRequestId(), NextRequestId(), security.UserName,
            OctetString.Empty, variables, security.Privacy, 65535, report), security.Registry);
    }

    private async Task<(ISnmpMessage Request, UserRegistry Registry)> CreateV3SetRequestAsync(
        IPEndPoint endpoint, Credential credential, IList<Variable> variables, CancellationToken token)
    {
        if (credential.Version != SnmpVersion.V3)
            return (new SetRequestMessage(NextRequestId(), ToVersion(credential.Version),
                new OctetString(GetCommunity(credential)), variables), new UserRegistry());

        var security = CreateV3Security(credential);
        var report = await Messenger.GetNextDiscovery(SnmpType.SetRequestPdu).GetResponseAsync(endpoint, token);
        return (new SetRequestMessage(VersionCode.V3, NextRequestId(), NextRequestId(), security.UserName,
            OctetString.Empty, variables, security.Privacy, 65535, report), security.Registry);
    }

    private (OctetString UserName, IPrivacyProvider Privacy, UserRegistry Registry) CreateV3Security(
        Credential credential)
    {
        if (!TestCredential(credential))
            throw new ArgumentException("A complete SNMPv3 credential is required.", nameof(credential));

        var authSecret = new OctetString(_credentialProtector.Unprotect(credential.ProtectedAuthPassword!));
        IAuthenticationProvider authentication = credential.AuthProtocol!.Trim().ToUpperInvariant() switch
        {
            "SHA256" or "SHA-256" => new SHA256AuthenticationProvider(authSecret),
            _ => throw new ArgumentException("Unsupported SNMPv3 authentication protocol.", nameof(credential))
        };
        var privacySecret = new OctetString(_credentialProtector.Unprotect(credential.ProtectedPrivPassword!));
        IPrivacyProvider privacy = credential.PrivProtocol!.Trim().ToUpperInvariant() switch
        {
            "AES" or "AES128" or "AES-128" => new AESPrivacyProvider(privacySecret, authentication),
            "AES192" or "AES-192" => new AES192PrivacyProvider(privacySecret, authentication),
            "AES256" or "AES-256" => new AES256PrivacyProvider(privacySecret, authentication),
            _ => throw new ArgumentException("Unsupported SNMPv3 privacy protocol.", nameof(credential))
        };
        var userName = new OctetString(credential.UserName!);
        var registry = new UserRegistry();
        registry.Add(userName, privacy);
        return (userName, privacy, registry);
    }

    private static SnmpResult Failure(string message, int errorStatus, TimeSpan elapsed) =>
        new SnmpResult(false, null, errorStatus, 0, message, elapsed);

    private static int NextRequestId() => Interlocked.Increment(ref _requestId);
}
