using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

public class NotificationChannel
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public NotificationChannelType Type { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";
    public bool IsEnabled { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private NotificationChannel() { }

    public NotificationChannel(Guid id, string name, NotificationChannelType type,
        string configurationJson)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        ConfigurationJson = configurationJson ?? "{}";
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static NotificationChannel Create(string name, NotificationChannelType type,
        string configurationJson = "{}")
        => new(Guid.NewGuid(), name, type, configurationJson);
}