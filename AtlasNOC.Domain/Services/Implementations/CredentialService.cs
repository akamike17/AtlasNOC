using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services;

public class CredentialService : ICredentialService
{
    private readonly IRepository<Credential> _repository;
    private readonly IAuditService _auditService;
    private readonly ICredentialProtector _credentialProtector;

    public CredentialService(IRepository<Credential> repository, IAuditService auditService,
        ICredentialProtector credentialProtector)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _credentialProtector = credentialProtector ?? throw new ArgumentNullException(nameof(credentialProtector));
    }

    public async Task<Credential> CreateV2cAsync(string name, string community,
        string createdBy, DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var protectedCommunity = _credentialProtector.Protect(community);
        var credential = Credential.CreateV2c(name, protectedCommunity, createdBy, expiresAt);
        await _repository.AddAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Create", createdBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            newValue: $"Version=V2c, Name={name}", cancellationToken: cancellationToken);
        return credential;
    }

    public async Task<Credential> CreateV3Async(string name, string userName,
        string authProtocol, string authPassword, string privProtocol,
        string privPassword, string createdBy, DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        ValidateV3Secret(authPassword, nameof(authPassword));
        ValidateV3Secret(privPassword, nameof(privPassword));
        authProtocol = NormalizeAuthProtocol(authProtocol);
        privProtocol = NormalizePrivProtocol(privProtocol);
        var credential = Credential.CreateV3(name, userName, authProtocol,
            _credentialProtector.Protect(authPassword), privProtocol,
            _credentialProtector.Protect(privPassword), createdBy, expiresAt);
        await _repository.AddAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Create", createdBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            newValue: $"Version=V3, User={userName}, AuthProtocol={authProtocol}",
            cancellationToken: cancellationToken);
        return credential;
    }

    public Task<IReadOnlyList<Credential>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Credential>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(c => c.IsActive).ToList().AsReadOnly();
    }

    public Task<Credential?> GetByIdAsync(CredentialId id,
        CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id.Value, cancellationToken);

    public async Task<IReadOnlyList<Credential>> GetExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(c => c.IsExpired).ToList().AsReadOnly();
    }

    public async Task RotateV2cAsync(CredentialId id, string newCommunity,
        string rotatedBy, CancellationToken cancellationToken = default)
    {
        var credential = await _repository.GetByIdAsync(id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Credential {id} not found");
        if (credential.Version != SnmpVersion.V2c)
            throw new InvalidOperationException("Credential is not V2c");

        var protectedCommunity = _credentialProtector.Protect(newCommunity);
        credential.Rotate(protectedCommunity, null, null, rotatedBy);
        await _repository.UpdateAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Rotate", rotatedBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            newValue: "Rotated V2c community", cancellationToken: cancellationToken);
    }

    public async Task RotateV3AuthAsync(CredentialId id, string newAuthPassword,
        string rotatedBy, CancellationToken cancellationToken = default)
    {
        var credential = await _repository.GetByIdAsync(id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Credential {id} not found");
        if (credential.Version != SnmpVersion.V3)
            throw new InvalidOperationException("Credential is not V3");
        ValidateV3Secret(newAuthPassword, nameof(newAuthPassword));
        credential.Rotate(null, _credentialProtector.Protect(newAuthPassword), null, rotatedBy);
        await _repository.UpdateAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Rotate", rotatedBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            newValue: "Rotated V3 auth password", cancellationToken: cancellationToken);
    }

    public async Task RotateV3PrivAsync(CredentialId id, string newPrivPassword,
        string rotatedBy, CancellationToken cancellationToken = default)
    {
        var credential = await _repository.GetByIdAsync(id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Credential {id} not found");
        if (credential.Version != SnmpVersion.V3)
            throw new InvalidOperationException("Credential is not V3");
        ValidateV3Secret(newPrivPassword, nameof(newPrivPassword));
        credential.Rotate(null, null, _credentialProtector.Protect(newPrivPassword), rotatedBy);
        await _repository.UpdateAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Rotate", rotatedBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            newValue: "Rotated V3 priv password", cancellationToken: cancellationToken);
    }

    public async Task DeactivateAsync(CredentialId id, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var credential = await _repository.GetByIdAsync(id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Credential {id} not found");
        credential.Deactivate(modifiedBy);
        await _repository.UpdateAsync(credential, cancellationToken);
        await _auditService.LogSuccessAsync("Credential", "Deactivate", modifiedBy,
            targetResource: credential.Id.Value.ToString(), targetResourceType: nameof(Credential),
            cancellationToken: cancellationToken);
    }

    private static void ValidateV3Secret(string secret, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 8)
            throw new ArgumentException("SNMPv3 passphrases must contain at least 8 characters.", parameterName);
    }

    private static string NormalizeAuthProtocol(string protocol) => protocol.Trim().ToUpperInvariant() switch
    {
        "SHA256" or "SHA-256" => "SHA256",
        _ => throw new ArgumentException("Only SHA-256 is supported for SNMPv3 authentication.", nameof(protocol))
    };

    private static string NormalizePrivProtocol(string protocol) => protocol.Trim().ToUpperInvariant() switch
    {
        "AES" or "AES128" or "AES-128" => "AES128",
        "AES192" or "AES-192" => "AES192",
        "AES256" or "AES-256" => "AES256",
        _ => throw new ArgumentException("Only AES privacy protocols are supported for SNMPv3.", nameof(protocol))
    };
}
