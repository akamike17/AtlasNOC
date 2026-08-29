namespace AtlasNOC.Domain.Entities;

/// <summary>Perfil de polling: frecuencia de cada tipo de adquisición.</summary>
public class PollingProfile
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int IcmpIntervalSeconds { get; private set; } = 30;
    public int HealthIntervalSeconds { get; private set; } = 60;
    public int InterfaceIntervalSeconds { get; private set; } = 60;
    public int InventoryIntervalMinutes { get; private set; } = 15;
    public int NeighborIntervalMinutes { get; private set; } = 5;
    public int TimeoutMs { get; private set; } = 5000;
    public int RetryCount { get; private set; } = 1;
    public bool IsDefault { get; private set; }

    private PollingProfile() { }

    public PollingProfile(string name, int icmpIntervalSeconds = 30,
        int healthIntervalSeconds = 60, int interfaceIntervalSeconds = 60,
        int inventoryIntervalMinutes = 15, int neighborIntervalMinutes = 5,
        int timeoutMs = 5000, int retryCount = 1, bool isDefault = false)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IcmpIntervalSeconds = icmpIntervalSeconds;
        HealthIntervalSeconds = healthIntervalSeconds;
        InterfaceIntervalSeconds = interfaceIntervalSeconds;
        InventoryIntervalMinutes = inventoryIntervalMinutes;
        NeighborIntervalMinutes = neighborIntervalMinutes;
        TimeoutMs = timeoutMs;
        RetryCount = retryCount;
        IsDefault = isDefault;
    }
}