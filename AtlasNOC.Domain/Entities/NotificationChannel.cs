using System;
using System.Collections.Generic;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

public sealed class NotificationChannel
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public NotificationChannelType Type { get; private set; }
    public IDictionary<string, string> Configuration { get; private set; } = new Dictionary<string, string>();
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private NotificationChannel() { } // EF Core

    public NotificationChannel(
        string name,
        NotificationChannelType type,
        IDictionary<string, string> configuration,
        bool isEnabled = true)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        IsEnabled = isEnabled;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateConfiguration(IDictionary<string, string> configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}