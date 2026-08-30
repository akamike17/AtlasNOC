using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Identity;
using AtlasNOC.Domain.ValueObjects;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AtlasNOC.Infrastructure.Persistence;

public class AtlasNOCDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>,
    IDataProtectionKeyContext
{
    public AtlasNOCDbContext(DbContextOptions<AtlasNOCDbContext> options) : base(options) { }

    public DbSet<WispOrganization> Organizations => Set<WispOrganization>();
    public DbSet<NetworkSite> Sites => Set<NetworkSite>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<ServiceEndpoint> ServiceEndpoints => Set<ServiceEndpoint>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceInterface> DeviceInterfaces => Set<DeviceInterface>();
    public DbSet<NetworkLink> NetworkLinks => Set<NetworkLink>();
    public DbSet<NeighborObservation> NeighborObservations => Set<NeighborObservation>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<DeviceCapability> DeviceCapabilities => Set<DeviceCapability>();
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();
    public DbSet<RadioSector> RadioSectors => Set<RadioSector>();
    public DbSet<WirelessAssociation> WirelessAssociations => Set<WirelessAssociation>();
    public DbSet<PollingProfile> PollingProfiles => Set<PollingProfile>();
    public DbSet<MetricSample> MetricSamples => Set<MetricSample>();
    public DbSet<DeviceStateEvent> DeviceStateEvents => Set<DeviceStateEvent>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Shared Guid-backed value-object converters ─────────────────────
        var deviceIdConv = new ValueConverter<DeviceId, Guid>(v => v.Value, v => DeviceId.From(v));
        var siteIdConv = new ValueConverter<SiteId, Guid>(v => v.Value, v => SiteId.From(v));
        var siteIdNullableConv = new ValueConverter<SiteId?, Guid>(v => v == null ? Guid.Empty : v.Value, v => v == Guid.Empty ? null : SiteId.From(v));
        var interfaceIdConv = new ValueConverter<InterfaceId, Guid>(v => v.Value, v => InterfaceId.From(v));
        var linkIdConv = new ValueConverter<LinkId, Guid>(v => v.Value, v => LinkId.From(v));
        var alertIdConv = new ValueConverter<AlertId, Guid>(v => v.Value, v => AlertId.From(v));
        var credentialIdConv = new ValueConverter<CredentialId, Guid>(v => v.Value, v => CredentialId.From(v));
        var orgIdConv = new ValueConverter<OrganizationId, Guid>(v => v.Value, v => OrganizationId.From(v));

        // ─── Organization ───────────────────────────────────────────────────
        modelBuilder.Entity<WispOrganization>(e =>
        {
            e.ToTable("Organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(orgIdConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
        });

        // ─── Site ───────────────────────────────────────────────────────────
        modelBuilder.Entity<NetworkSite>(e =>
        {
            e.ToTable("Sites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(siteIdConv);
            e.Property(x => x.OrganizationId).HasConversion(orgIdConv);
            e.Property(x => x.ParentSiteId).HasConversion(siteIdNullableConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).IsRequired().HasMaxLength(50);
            e.Property(x => x.SiteType).HasConversion<int>();
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });

        // ─── Subscriber / ServiceEndpoint ───────────────────────────────────
        modelBuilder.Entity<Subscriber>(e =>
        {
            e.ToTable("Subscribers");
            e.HasKey(x => x.Id);
            e.Property(x => x.OrganizationId).HasConversion(orgIdConv);
            e.Property(x => x.SiteId).HasConversion(siteIdNullableConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<ServiceEndpoint>(e =>
        {
            e.ToTable("ServiceEndpoints");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        // ─── Device ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(deviceIdConv);
            e.Property(x => x.SiteId).HasConversion(siteIdNullableConv);
            e.Property(x => x.Hostname).IsRequired().HasMaxLength(200);
            e.Property(x => x.ManagementIp).IsRequired().HasMaxLength(45);
            e.Property(x => x.DeviceType).HasConversion<int>();
            e.Property(x => x.Vendor).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.SerialNumber).HasMaxLength(200);
            e.Property(x => x.FirmwareVersion).HasMaxLength(100);
            e.Property(x => x.DriverKey).HasMaxLength(64);
            e.HasIndex(x => x.ManagementIp).IsUnique();
        });

        // ─── Interface ──────────────────────────────────────────────────────
        modelBuilder.Entity<DeviceInterface>(e =>
        {
            e.ToTable("DeviceInterfaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(interfaceIdConv);
            e.Property(x => x.DeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.MacAddress).HasMaxLength(32);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.AdminStatus).HasConversion<int>();
            e.Property(x => x.OperStatus).HasConversion<int>();
            e.Property(x => x.InterfaceType).HasMaxLength(64);
            e.HasIndex(x => new { x.DeviceId, x.IfIndex }).IsUnique();
        });

        // ─── Link ───────────────────────────────────────────────────────────
        modelBuilder.Entity<NetworkLink>(e =>
        {
            e.ToTable("NetworkLinks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(linkIdConv);
            e.Property(x => x.AInterfaceId).HasConversion(interfaceIdConv);
            e.Property(x => x.BInterfaceId).HasConversion(interfaceIdConv);
            e.Property(x => x.LinkType).HasConversion<int>();
            e.Property(x => x.DiscoverySource).HasConversion<int>();
            e.Property(x => x.AdminStatus).HasConversion<int>();
            e.Property(x => x.OperStatus).HasConversion<int>();
            e.HasIndex(x => x.AInterfaceId);
            e.HasIndex(x => x.BInterfaceId);
        });

        // ─── NeighborObservation ────────────────────────────────────────────
        modelBuilder.Entity<NeighborObservation>(e =>
        {
            e.ToTable("NeighborObservations");
            e.HasKey(x => x.Id);
            e.Property(x => x.LocalDeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.LocalInterfaceId).HasConversion(interfaceIdConv);
            e.Property(x => x.RemoteIdentity).IsRequired().HasMaxLength(200);
            e.Property(x => x.RemotePortIdentity).HasMaxLength(200);
            e.Property(x => x.Protocol).HasConversion<int>();
            e.Property(x => x.RawEvidenceHash).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.RawEvidenceHash);
        });

        // ─── Credential ─────────────────────────────────────────────────────
        modelBuilder.Entity<DeviceCredential>(e =>
        {
            e.ToTable("DeviceCredentials");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(credentialIdConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.SnmpVersion).HasConversion<int>();
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.AuthProtocol).HasMaxLength(50);
            e.Property(x => x.PrivProtocol).HasMaxLength(50);
            // Secrets stored encrypted/ciphered — column length must accommodate the protector output.
            e.Property(x => x.CommunityProtected).HasMaxLength(1024);
            e.Property(x => x.AuthPasswordProtected).HasMaxLength(1024);
            e.Property(x => x.PrivPasswordProtected).HasMaxLength(1024);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ─── Capability ─────────────────────────────────────────────────────
        modelBuilder.Entity<DeviceCapability>(e =>
        {
            e.ToTable("DeviceCapabilities");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.CapabilityKey).IsRequired().HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(500);
        });

        // ─── DiscoveryRun ───────────────────────────────────────────────────
        modelBuilder.Entity<DiscoveryRun>(e =>
        {
            e.ToTable("DiscoveryRuns");
            e.HasKey(x => x.Id);
            e.Property(x => x.ScopeIp).IsRequired().HasMaxLength(100);
            e.Property(x => x.TargetSiteId).HasMaxLength(64);
            e.Property(x => x.CredentialId).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.SummaryJson).HasColumnType("longtext");
            e.HasIndex(x => x.StartedAtUtc);
        });

        // ─── RadioSector / WirelessAssociation ──────────────────────────────
        modelBuilder.Entity<RadioSector>(e =>
        {
            e.ToTable("RadioSectors");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Ssid).HasMaxLength(100);
        });

        modelBuilder.Entity<WirelessAssociation>(e =>
        {
            e.ToTable("WirelessAssociations");
            e.HasKey(x => x.Id);
            e.Property(x => x.ApDeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.CpeDeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.SectorName).HasMaxLength(200);
            e.HasIndex(x => x.ApDeviceId);
            e.HasIndex(x => x.CpeDeviceId);
        });

        // ─── PollingProfile ─────────────────────────────────────────────────
        modelBuilder.Entity<PollingProfile>(e =>
        {
            e.ToTable("PollingProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        // ─── MetricSample ───────────────────────────────────────────────────
        modelBuilder.Entity<MetricSample>(e =>
        {
            e.ToTable("MetricSamples");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.ResourceType).IsRequired().HasMaxLength(32);
            e.Property(x => x.ResourceId).IsRequired().HasMaxLength(64);
            e.Property(x => x.MetricName).IsRequired().HasMaxLength(64);
            e.Property(x => x.ValueDouble).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(32);
            e.Property(x => x.Quality).HasMaxLength(32);
            e.HasIndex(x => new { x.ResourceType, x.ResourceId, x.MetricName, x.TimestampUtc });
        });

        // ─── DeviceStateEvent ───────────────────────────────────────────────
        modelBuilder.Entity<DeviceStateEvent>(e =>
        {
            e.ToTable("DeviceStateEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasConversion(deviceIdConv);
            e.Property(x => x.FromStatus).HasConversion<int>();
            e.Property(x => x.ToStatus).HasConversion<int>();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc });
        });

        // ─── AlertRule / Alert ──────────────────────────────────────────────
        modelBuilder.Entity<AlertRule>(e =>
        {
            e.ToTable("AlertRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.MetricName).IsRequired().HasMaxLength(64);
            e.Property(x => x.ComparisonOperator).IsRequired().HasMaxLength(8);
            e.Property(x => x.Severity).HasConversion<int>();
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.ToTable("Alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(alertIdConv);
            e.Property(x => x.ResourceType).IsRequired().HasMaxLength(32);
            e.Property(x => x.ResourceId).IsRequired().HasMaxLength(64);
            e.Property(x => x.MetricName).IsRequired().HasMaxLength(64);
            e.Property(x => x.Severity).HasConversion<int>();
            e.Property(x => x.State).HasConversion<int>();
            e.Property(x => x.Evidence).HasMaxLength(4000);
            e.HasIndex(x => x.State);
            e.HasIndex(x => x.ResourceId);
        });

        // ─── Incident ───────────────────────────────────────────────────────
        modelBuilder.Entity<Incident>(e =>
        {
            e.ToTable("Incidents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.RootCauseDeviceId).HasMaxLength(64);
            e.HasIndex(x => x.Status);
        });

        // ─── ApiKey ─────────────────────────────────────────────────────────
        modelBuilder.Entity<ApiKey>(e =>
        {
            e.ToTable("ApiKeys");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(64);
            e.Property(x => x.KeyHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.KeyPrefix).IsRequired().HasMaxLength(16);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Scopes).HasMaxLength(500);
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        // ─── AuditEvent ─────────────────────────────────────────────────────
        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("AuditEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).IsRequired().HasMaxLength(100);
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.ActorUserId).IsRequired().HasMaxLength(64);
            e.Property(x => x.ActorEmail).HasMaxLength(255);
            e.Property(x => x.ActorRole).HasMaxLength(100);
            e.Property(x => x.TargetResource).HasMaxLength(255);
            e.Property(x => x.TargetResourceType).HasMaxLength(100);
            e.Property(x => x.OldValue).HasMaxLength(2000);
            e.Property(x => x.NewValue).HasMaxLength(2000);
            e.Property(x => x.Result).HasConversion<int>();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.HasIndex(x => x.TimestampUtc);
            e.HasIndex(x => x.Category);
        });

        // ─── NotificationChannel ────────────────────────────────────────────
        modelBuilder.Entity<NotificationChannel>(e =>
        {
            e.ToTable("NotificationChannels");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.ConfigurationJson).HasColumnType("longtext");
            e.HasIndex(x => x.Name).IsUnique();
        });
    }
}