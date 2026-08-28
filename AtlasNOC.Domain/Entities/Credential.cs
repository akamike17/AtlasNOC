using System;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.ValueObjects;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtlasNOC.Domain.Entities;

public class Credential
{
    public CredentialId Id { get; internal set; } = null!;
    public string Name { get; internal set; } = string.Empty;
    public SnmpVersion Version { get; internal set; }
    [JsonIgnore]
    public string? Community { get; internal set; }
    public string? UserName { get; internal set; }
    public string? AuthProtocol { get; internal set; }
    [JsonIgnore]
    [Column("AuthPasswordHashEncoded")]
    public string? ProtectedAuthPassword { get; internal set; }
    public string? PrivProtocol { get; internal set; }
    [JsonIgnore]
    [Column("PrivPasswordHashEncoded")]
    public string? ProtectedPrivPassword { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime? LastRotatedAt { get; internal set; }
    public bool IsActive { get; internal set; }
    public DateTime? ExpiresAt { get; internal set; }
    public string CreatedBy { get; internal set; } = string.Empty;
    public string? ModifiedBy { get; internal set; }

    private Credential() { }

    public Credential(CredentialId id, string name, SnmpVersion version, string createdBy,
        string? community = null, string? userName = null, string? authProtocol = null,
        string? protectedAuthPassword = null, string? privProtocol = null,
        string? protectedPrivPassword = null, DateTime? expiresAt = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version;
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        if (version == SnmpVersion.V1 || version == SnmpVersion.V2c)
        {
            Community = community;
        }
        else if (version == SnmpVersion.V3)
        {
            UserName = userName;
            AuthProtocol = authProtocol;
            ProtectedAuthPassword = protectedAuthPassword;
            PrivProtocol = privProtocol;
            ProtectedPrivPassword = protectedPrivPassword;
        }
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public static Credential CreateV2c(string name, string community, string createdBy,
        DateTime? expiresAt = null)
        => new(CredentialId.New(), name, SnmpVersion.V2c, createdBy,
            community: community, expiresAt: expiresAt);

    public static Credential CreateV3(string name, string userName, string authProtocol,
        string protectedAuthPassword, string privProtocol, string protectedPrivPassword,
        string createdBy, DateTime? expiresAt = null)
        => new(CredentialId.New(), name, SnmpVersion.V3, createdBy,
            userName: userName, authProtocol: authProtocol, protectedAuthPassword: protectedAuthPassword,
            privProtocol: privProtocol, protectedPrivPassword: protectedPrivPassword,
            expiresAt: expiresAt);

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public void Rotate(string? newCommunity, string? newProtectedAuthPassword,
        string? newProtectedPrivPassword, string rotatedBy)
    {
        EntityExtensions.Rotate(this, newCommunity, newProtectedAuthPassword, newProtectedPrivPassword, rotatedBy);
    }

    public void Deactivate(string modifiedBy)
    {
        EntityExtensions.Deactivate(this, modifiedBy);
    }

    public bool CanUse => IsActive && !IsExpired;
}
