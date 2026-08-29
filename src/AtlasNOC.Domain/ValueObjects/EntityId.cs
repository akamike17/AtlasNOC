namespace AtlasNOC.Domain.ValueObjects;

/// <summary>Base structural type for strongly-typed identifiers backed by a Guid.</summary>
public abstract record EntityId
{
    public Guid Value { get; }

    protected EntityId(Guid value) : this(value, nameof(value)) { }

    protected EntityId(Guid value, string argumentName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{argumentName} cannot be empty", argumentName);
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(EntityId id) => id.Value;
}

public sealed record DeviceId : EntityId
{
    public DeviceId(Guid value) : base(value, nameof(value)) { }
    public static DeviceId New() => new(Guid.NewGuid());
    public static DeviceId From(Guid value) => new(value);
    public static implicit operator DeviceId(Guid value) => new(value);
}

public sealed record SiteId : EntityId
{
    public SiteId(Guid value) : base(value, nameof(value)) { }
    public static SiteId New() => new(Guid.NewGuid());
    public static SiteId From(Guid value) => new(value);
    public static implicit operator SiteId(Guid value) => new(value);
}

public sealed record InterfaceId : EntityId
{
    public InterfaceId(Guid value) : base(value, nameof(value)) { }
    public static InterfaceId New() => new(Guid.NewGuid());
    public static InterfaceId From(Guid value) => new(value);
    public static implicit operator InterfaceId(Guid value) => new(value);
}

public sealed record LinkId : EntityId
{
    public LinkId(Guid value) : base(value, nameof(value)) { }
    public static LinkId New() => new(Guid.NewGuid());
    public static LinkId From(Guid value) => new(value);
    public static implicit operator LinkId(Guid value) => new(value);
}

public sealed record AlertId : EntityId
{
    public AlertId(Guid value) : base(value, nameof(value)) { }
    public static AlertId New() => new(Guid.NewGuid());
    public static AlertId From(Guid value) => new(value);
    public static implicit operator AlertId(Guid value) => new(value);
}

public sealed record CredentialId : EntityId
{
    public CredentialId(Guid value) : base(value, nameof(value)) { }
    public static CredentialId New() => new(Guid.NewGuid());
    public static CredentialId From(Guid value) => new(value);
    public static implicit operator CredentialId(Guid value) => new(value);
}

public sealed record OrganizationId : EntityId
{
    public OrganizationId(Guid value) : base(value, nameof(value)) { }
    public static OrganizationId New() => new(Guid.NewGuid());
    public static OrganizationId From(Guid value) => new(value);
    public static implicit operator OrganizationId(Guid value) => new(value);
}