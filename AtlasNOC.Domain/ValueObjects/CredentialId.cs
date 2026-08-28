using System;

namespace AtlasNOC.Domain.ValueObjects;

public record CredentialId
{
    public Guid Value { get; }

    public CredentialId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CredentialId cannot be empty", nameof(value));
        Value = value;
    }

    public static CredentialId New() => new(Guid.NewGuid());
    public static CredentialId From(Guid value) => new(value);
    public static implicit operator Guid(CredentialId id) => id.Value;
    public static implicit operator CredentialId(Guid value) => new(value);
}
