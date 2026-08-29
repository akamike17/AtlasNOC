using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Punto de servicio entregado a un suscriptor (relación CPE↔cliente).</summary>
public class ServiceEndpoint
{
    public Guid Id { get; private set; }
    public Guid SubscriberId { get; private set; }
    public DeviceId DeviceId { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private ServiceEndpoint() { }

    public ServiceEndpoint(Guid subscriberId, DeviceId deviceId, string? description = null)
    {
        Id = Guid.NewGuid();
        SubscriberId = subscriberId;
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
    }
}