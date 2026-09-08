namespace AtlasNOC.Domain.Entities;

/// <summary>Perfil de polling: frecuencia de cada tipo de adquisición.</summary>
public class PollingProfile
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int IcmpIntervalSeconds { get; private set; } = 30;
    public int HealthIntervalSeconds { get; private set; } = 60;
    public int InterfaceIntervalSeconds { get; private set; } = 60;
    public int WirelessIntervalSeconds { get; private set; } = 60;
    public int InventoryIntervalMinutes { get; private set; } = 15;
    public int NeighborIntervalMinutes { get; private set; } = 5;
    public int TimeoutMs { get; private set; } = 5000;
    public int RetryCount { get; private set; } = 1;
    public bool IsDefault { get; private set; }

    private PollingProfile() { }

    public PollingProfile(string name, int icmpIntervalSeconds = 30,
        int healthIntervalSeconds = 60, int interfaceIntervalSeconds = 60,
        int wirelessIntervalSeconds = 60,
        int inventoryIntervalMinutes = 15, int neighborIntervalMinutes = 5,
        int timeoutMs = 5000, int retryCount = 1, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (icmpIntervalSeconds <= 0 || healthIntervalSeconds <= 0 || interfaceIntervalSeconds <= 0
            || wirelessIntervalSeconds <= 0 || timeoutMs <= 0 || retryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(icmpIntervalSeconds), "Los intervalos/timeout deben ser positivos y retries no negativo.");
        Id = Guid.NewGuid();
        Name = name.Trim();
        IcmpIntervalSeconds = icmpIntervalSeconds;
        HealthIntervalSeconds = healthIntervalSeconds;
        InterfaceIntervalSeconds = interfaceIntervalSeconds;
        WirelessIntervalSeconds = wirelessIntervalSeconds;
        InventoryIntervalMinutes = inventoryIntervalMinutes;
        NeighborIntervalMinutes = neighborIntervalMinutes;
        TimeoutMs = timeoutMs;
        RetryCount = retryCount;
        IsDefault = isDefault;
    }
}
