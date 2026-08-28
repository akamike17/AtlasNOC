using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface ICredentialService
{
    Task<Credential> CreateV2cAsync(string name, string community, string createdBy,
        DateTime? expiresAt = null, CancellationToken cancellationToken = default);
    Task<Credential> CreateV3Async(string name, string userName, string authProtocol,
        string authPassword, string privProtocol, string privPassword,
        string createdBy, DateTime? expiresAt = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Credential>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Credential>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Credential?> GetByIdAsync(CredentialId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Credential>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task RotateV2cAsync(CredentialId id, string newCommunity, string rotatedBy,
        CancellationToken cancellationToken = default);
    Task RotateV3AuthAsync(CredentialId id, string newAuthPassword, string rotatedBy,
        CancellationToken cancellationToken = default);
    Task RotateV3PrivAsync(CredentialId id, string newPrivPassword, string rotatedBy,
        CancellationToken cancellationToken = default);
    Task DeactivateAsync(CredentialId id, string modifiedBy,
        CancellationToken cancellationToken = default);
}
