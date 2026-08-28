using System;
using System.ComponentModel.DataAnnotations;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Web.Models;

public class CreateDeviceRequest
{
    [Required][MaxLength(100)] public string Name { get; set; } = "";
    [Required]
    [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?$",
        ErrorMessage = "Invalid IPv4 address")]
    public string IpAddress { get; set; } = "";
    [Required] public DeviceType Type { get; set; }
    [Required][MinLength(1)] public string CreatedBy { get; set; } = "";
    [MaxLength(100)] public string? Location { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
}

public class UpdateStatusRequest
{
    [Required] public DeviceStatus Status { get; set; }
    [Required][MinLength(1)] public string ModifiedBy { get; set; } = "";
}

public class UpdateDetailsRequest
{
    [Required][MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(100)] public string? Location { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    [Required][MinLength(1)] public string ModifiedBy { get; set; } = "";
}

public class CreateV2cRequest
{
    [Required][MaxLength(100)] public string Name { get; set; } = "";
    [Required][MaxLength(200)] public string Community { get; set; } = "";
    [Required][MinLength(1)] public string CreatedBy { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

public class CreateV3Request
{
    [Required][MaxLength(100)] public string Name { get; set; } = "";
    [Required][MaxLength(100)] public string UserName { get; set; } = "";
    [Required][MaxLength(20)] public string AuthProtocol { get; set; } = "";
    [Required][MinLength(8)][MaxLength(256)] public string AuthPassword { get; set; } = "";
    [Required][MaxLength(20)] public string PrivProtocol { get; set; } = "";
    [Required][MinLength(8)][MaxLength(256)] public string PrivPassword { get; set; } = "";
    [Required][MinLength(1)] public string CreatedBy { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

public class RotateV2cRequest
{
    [Required][MinLength(1)] public string NewCommunity { get; set; } = "";
    [Required][MinLength(1)] public string RotatedBy { get; set; } = "";
}

public class RotateV3AuthRequest
{
    [Required][MinLength(8)][MaxLength(256)] public string NewAuthPassword { get; set; } = "";
    [Required][MinLength(1)] public string RotatedBy { get; set; } = "";
}

public class RotateV3PrivRequest
{
    [Required][MinLength(8)][MaxLength(256)] public string NewPrivPassword { get; set; } = "";
    [Required][MinLength(1)] public string RotatedBy { get; set; } = "";
}

public class CreateAlertRequest
{
    [Required] public Guid DeviceId { get; set; }
    [Required][MaxLength(500)] public string Message { get; set; } = "";
    [Required] public AlertSeverity Severity { get; set; }
    [MaxLength(100)] public string? Source { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class AcknowledgeRequest
{
    [Required][MinLength(1)] public string AcknowledgedBy { get; set; } = "";
}

public class ResolveRequest
{
    [Required][MinLength(1)] public string ResolvedBy { get; set; } = "";
    [MaxLength(500)] public string? Notes { get; set; }
}

public class CreateIncidentRequest
{
    [Required][MaxLength(200)] public string Title { get; set; } = "";
    [Required][MaxLength(1000)] public string Description { get; set; } = "";
    [Required][MinLength(1)] public string CreatedBy { get; set; } = "";
}

public class ResolveIncidentRequest
{
    [Required][MinLength(1)] public string ResolvedBy { get; set; } = "";
    [MaxLength(500)] public string? Notes { get; set; }
}
