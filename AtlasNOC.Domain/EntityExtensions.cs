using System;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain;

/// <summary>
/// Extensión para mutar entidades que usan propiedades init-only.
/// Usa campos de backing respetando el diseño de dominio; no es reflection-based.
/// </summary>
public static class EntityExtensions
{
    // ─── Device ──────────────────────────────────────────────────────────────

    public static void RecordStatusChange(this Device device, DeviceStatus newStatus, string modifiedBy)
    {
        device.Status = newStatus;
        device.UpdatedAt = DateTime.UtcNow;
        device.ModifiedBy = modifiedBy;
    }

    public static void RecordLastChecked(this Device device)
    {
        device.LastCheckedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
    }

    public static void Deactivate(this Device device, string modifiedBy)
    {
        device.IsActive = false;
        device.UpdatedAt = DateTime.UtcNow;
        device.ModifiedBy = modifiedBy;
    }

    public static void Reactivate(this Device device, string modifiedBy)
    {
        device.IsActive = true;
        device.UpdatedAt = DateTime.UtcNow;
        device.ModifiedBy = modifiedBy;
    }

    public static void UpdateDetails(this Device device, string name, string? location,
        string? description, string modifiedBy)
    {
        device.Name = name;
        device.Location = location;
        device.Description = description;
        device.UpdatedAt = DateTime.UtcNow;
        device.ModifiedBy = modifiedBy;
    }

    // ─── Credential ──────────────────────────────────────────────────────────

    public static void Rotate(this Credential credential, string? newCommunity,
        string? newProtectedAuthPassword, string? newProtectedPrivPassword, string rotatedBy)
    {
        if (credential.Version == SnmpVersion.V1 || credential.Version == SnmpVersion.V2c)
        {
            credential.Community = newCommunity;
        }
        else if (credential.Version == SnmpVersion.V3)
        {
            if (newProtectedAuthPassword is not null)
                credential.ProtectedAuthPassword = newProtectedAuthPassword;
            if (newProtectedPrivPassword is not null)
                credential.ProtectedPrivPassword = newProtectedPrivPassword;
        }
        credential.LastRotatedAt = DateTime.UtcNow;
        credential.ModifiedBy = rotatedBy;
    }

    public static void Deactivate(this Credential credential, string modifiedBy)
    {
        credential.IsActive = false;
        credential.ModifiedBy = modifiedBy;
    }
}
