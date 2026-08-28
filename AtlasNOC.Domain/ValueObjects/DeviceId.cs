using System;

namespace AtlasNOC.Domain.ValueObjects;

public record DeviceId
{
    public Guid Value { get; }

    public DeviceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeviceId cannot be empty", nameof(value));
        Value = value;
    }

    public static DeviceId New() => new(Guid.NewGuid());
    public static DeviceId From(Guid value) => new(value);
    public static implicit operator Guid(DeviceId id) => id.Value;
    public static implicit operator DeviceId(Guid value) => new(value);
}
