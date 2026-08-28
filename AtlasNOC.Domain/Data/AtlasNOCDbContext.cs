using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Data;

public class AtlasNOCDbContext : DbContext, IDataProtectionKeyContext
{
    public DbSet<Device> Devices { get; set; }
    public DbSet<Credential> Credentials { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<AuditEvent> AuditEvents { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<CveRecord> CveRecords { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<NotificationChannel> NotificationChannels { get; set; }
    public DbSet<MetricSample> MetricSamples { get; set; }
    public DbSet<DiscoveryRun> DiscoveryRuns { get; set; }

    public AtlasNOCDbContext(DbContextOptions<AtlasNOCDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── ValueObject converters ──────────────────────────────────────────
        var deviceIdConverter = new ValueConverter<DeviceId, Guid>(
            value => value.Value,
            value => DeviceId.From(value));
        var alertIdConverter = new ValueConverter<AlertId, Guid>(
            value => value.Value,
            value => AlertId.From(value));
        var credentialIdConverter = new ValueConverter<CredentialId, Guid>(
            value => value.Value,
            value => CredentialId.From(value));

        // ─── Device ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired()
                .HasConversion(deviceIdConverter);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.LastCheckedAt).IsRequired(false);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ModifiedBy).IsRequired(false).HasMaxLength(255);
            entity.Ignore(e => e.Alerts);
            entity.Ignore("_alerts");
            entity.HasIndex(e => e.IpAddress).IsUnique();
        });

        // ─── Credential ────────────────────────────────────────────────────
        modelBuilder.Entity<Credential>(entity =>
        {
            entity.ToTable("Credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired()
                .HasConversion(credentialIdConverter);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Version).HasConversion<int>();
            entity.Property(e => e.Community).HasMaxLength(200);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.AuthProtocol).HasMaxLength(20);
            entity.Property(e => e.ProtectedAuthPassword).HasColumnName("AuthPasswordHash")
                .IsRequired(false).HasMaxLength(512);
            entity.Property(e => e.PrivProtocol).HasMaxLength(20);
            entity.Property(e => e.ProtectedPrivPassword).HasColumnName("PrivPasswordHash")
                .IsRequired(false).HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastRotatedAt).IsRequired(false);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired(false);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Ignore(e => e.IsExpired);
            entity.Ignore(e => e.CanUse);
        });

        // ─── Alert ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("Alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired()
                .HasConversion(alertIdConverter);
            entity.Property(e => e.DeviceId).IsRequired()
                .HasConversion(deviceIdConverter);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Severity).HasConversion<int>();
            entity.Property(e => e.OccurredAt).IsRequired();
            entity.Property(e => e.AcknowledgedAt).IsRequired(false);
            entity.Property(e => e.ResolvedAt).IsRequired(false);
            entity.Property(e => e.AcknowledgedBy).IsRequired(false).HasMaxLength(255);
            entity.Property(e => e.ResolvedBy).IsRequired(false).HasMaxLength(255);
            entity.Property(e => e.ResolutionNotes).IsRequired(false).HasMaxLength(500);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.ResolvedAt);
            entity.HasIndex(e => e.Severity);
            entity.Ignore(e => e.IsActive);
        });

        // ─── Incident ──────────────────────────────────────────────────────
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("Incidents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ResolvedAt).IsRequired(false);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ResolvedBy).IsRequired(false).HasMaxLength(255);
            entity.Property(e => e.ResolutionNotes).IsRequired(false).HasMaxLength(500);
            entity.Ignore(e => e.RelatedAlerts);
        });

        // ─── AuditEvent ────────────────────────────────────────────────────
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).IsRequired();
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.UserEmail).IsRequired(false).HasMaxLength(255);
            entity.Property(e => e.UserRole).IsRequired(false).HasMaxLength(100);
            entity.Property(e => e.TargetResource).IsRequired(false).HasMaxLength(255);
            entity.Property(e => e.TargetResourceType).IsRequired(false).HasMaxLength(100);
            entity.Property(e => e.OldValue).IsRequired(false).HasMaxLength(1000);
            entity.Property(e => e.NewValue).IsRequired(false).HasMaxLength(1000);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Result).HasConversion<int>();
            entity.Property(e => e.Reason).IsRequired(false).HasMaxLength(500);
            entity.Property(e => e.IpAddress).IsRequired(false).HasMaxLength(45);
            entity.Property(e => e.UserAgent).IsRequired(false).HasMaxLength(500);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
        });

        // ─── ApiKey ────────────────────────────────────────────────────────
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("ApiKeys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.Owner).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Description).IsRequired(false).HasMaxLength(500);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasIndex(e => e.Owner);
            entity.HasIndex(e => e.IsActive).HasFilter("`IsActive` = 1");
        });

        // ─── CveRecord ────────────────────────────────────────────────────────
        modelBuilder.Entity<CveRecord>(entity =>
        {
            entity.ToTable("CveRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.CveId).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SourceIdentifier).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PublishedDate).IsRequired();
            entity.Property(e => e.LastModifiedDate).IsRequired();
            entity.Property(e => e.VulnStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.CvssVersion).HasMaxLength(16);
            entity.Property(e => e.CvssBaseSeverity).HasMaxLength(16);
            entity.Property(e => e.CvssVectorString).HasMaxLength(256);
            entity.Property(e => e.Keywords).HasMaxLength(500);
            entity.Property(e => e.References).IsRequired().HasColumnType("json");
            entity.Property(e => e.Weaknesses).IsRequired().HasColumnType("json");
            entity.Property(e => e.Configurations).IsRequired().HasColumnType("json");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CveId).IsUnique();
            entity.HasIndex(e => e.LastModifiedDate);
            entity.HasIndex(e => e.CvssBaseSeverity);
            entity.HasIndex(e => e.Keywords);
        });

        // ─── NotificationChannel ────────────────────────────────────────────────
        modelBuilder.Entity<NotificationChannel>(entity =>
        {
            entity.ToTable("NotificationChannels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).HasConversion<int>();
            var configurationProperty = entity.Property(e => e.Configuration)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<IDictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
                )
                .HasColumnType("json");
            configurationProperty.Metadata.SetValueComparer(
                new ValueComparer<IDictionary<string, string>>(
                    (left, right) => left != null && right != null &&
                        left.OrderBy(pair => pair.Key).SequenceEqual(right.OrderBy(pair => pair.Key)),
                    value => value.OrderBy(pair => pair.Key)
                        .Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    value => new Dictionary<string, string>(value)));
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired(false);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<MetricSample>(entity =>
        {
            entity.ToTable("MetricSamples");
            entity.HasKey(sample => sample.Id);
            entity.Property(sample => sample.DeviceId).IsRequired().HasConversion(deviceIdConverter);
            entity.Property(sample => sample.Timestamp).IsRequired();
            entity.Property(sample => sample.Success).IsRequired();
            entity.Property(sample => sample.LatencyMs).IsRequired(false);
            entity.Property(sample => sample.AvailabilityPercent).IsRequired();
            entity.Property(sample => sample.InterfaceMetricsJson).HasColumnType("json");
            entity.Property(sample => sample.ErrorMessage).HasMaxLength(500);
            entity.HasOne<Device>().WithMany().HasForeignKey(sample => sample.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(sample => new { sample.DeviceId, sample.Timestamp });
            entity.HasIndex(sample => sample.Timestamp);
        });

        modelBuilder.Entity<DiscoveryRun>(entity =>
        {
            entity.ToTable("DiscoveryRuns");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.SubnetCidr).IsRequired().HasMaxLength(64);
            entity.Property(run => run.StartedAt).IsRequired();
            entity.Property(run => run.CompletedAt).IsRequired(false);
            entity.Property(run => run.Status).HasConversion<int>();
            entity.Property(run => run.DevicesJson).IsRequired().HasColumnType("json");
            entity.Property(run => run.ErrorMessage).HasMaxLength(1000);
            entity.HasIndex(run => run.StartedAt);
            entity.HasIndex(run => run.Status);
        });
    }
}
