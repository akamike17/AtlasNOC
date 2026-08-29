using System.ComponentModel.DataAnnotations;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "La API key es obligatoria.")]
    public string ApiKey { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public string? OwnerHint { get; set; }
}

public sealed class CreateApiKeyUiRequest
{
    [Required(ErrorMessage = "El propietario es obligatorio.")]
    [MaxLength(100)]
    public string Owner { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public string Role { get; set; } = "ReadOnly";
}

public sealed class CreateDeviceUiRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "La IP es obligatoria.")]
    [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
        ErrorMessage = "Dirección IPv4 inválida.")]
    public string IpAddress { get; set; } = "";

    [Required]
    public DeviceType Type { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public sealed class EditDeviceUiRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public sealed class StartDiscoveryUiRequest
{
    [Required(ErrorMessage = "El CIDR de subred es obligatorio.")]
    [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(/([0-9]|[12][0-9]|3[0-2]))$",
        ErrorMessage = "Subred CIDR inválida, ej. 192.168.1.0/24.")]
    public string SubnetCidr { get; set; } = "";

    public int MaxConcurrency { get; set; } = 50;
    public bool EnableLldp { get; set; } = true;
    public bool EnableCdp { get; set; } = true;
    public bool EnableArp { get; set; } = true;
    public IList<Guid> CredentialIds { get; set; } = new List<Guid>();
}

public sealed class CreateIncidentUiRequest
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(1000)]
    public string Description { get; set; } = "";
}

public sealed class CreateAlertUiRequest
{
    [Required]
    public Guid DeviceId { get; set; }

    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [MaxLength(500)]
    public string Message { get; set; } = "";

    [Required]
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    [MaxLength(100)]
    public string? Source { get; set; }
}

public sealed class CreateCredentialV2cUiRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "El community string es obligatorio.")]
    [MaxLength(200)]
    public string Community { get; set; } = "";

    public DateTime? ExpiresAt { get; set; }
}

public sealed class CreateCredentialV3UiRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [MaxLength(100)]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "El protocolo de autenticación es obligatorio.")]
    [MaxLength(20)]
    public string AuthProtocol { get; set; } = "SHA";

    [Required(ErrorMessage = "La contraseña de autenticación es obligatoria.")]
    [MinLength(8)]
    [MaxLength(256)]
    public string AuthPassword { get; set; } = "";

    [Required(ErrorMessage = "El protocolo de privacidad es obligatorio.")]
    [MaxLength(20)]
    public string PrivProtocol { get; set; } = "DES";

    [Required(ErrorMessage = "La contraseña de privacidad es obligatoria.")]
    [MinLength(8)]
    [MaxLength(256)]
    public string PrivPassword { get; set; } = "";

    public DateTime? ExpiresAt { get; set; }
}

public sealed class ResolveConfirmationRequest
{
    [MaxLength(500)]
    public string? Notes { get; set; }
}