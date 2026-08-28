using System;

namespace AtlasNOC.Domain.ValueObjects;

public record AlertId
{
    public Guid Value { get; }

    public AlertId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("AlertId cannot be empty", nameof(value));
        Value = value;
    }

    public static AlertId New() => new(Guid.NewGuid());
    public static AlertId From(Guid value) => new(value);
    public static implicit operator Guid(AlertId id) => id.Value;
    public static implicit operator AlertId(Guid value) => new(value);
}
