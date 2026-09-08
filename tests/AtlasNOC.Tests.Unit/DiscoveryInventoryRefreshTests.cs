using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class DiscoveryInventoryRefreshTests
{
    [Fact]
    public void Rediscovery_updates_non_empty_device_identity()
    {
        var device = new Device("old", "192.0.2.1", DeviceType.Unknown, Vendor.Generic, firmwareVersion: "1.0");
        device.UpdateDiscoveredIdentity("router-1", DeviceType.Router, Vendor.MikroTik, "RB5009", "ABC", "7.16", "mikrotik");
        Assert.Equal("router-1", device.Hostname);
        Assert.Equal("7.16", device.FirmwareVersion);
        Assert.Equal(Vendor.MikroTik, device.Vendor);
    }

    [Fact]
    public void Rediscovery_does_not_replace_known_values_with_empty_data()
    {
        var device = new Device("router-1", "192.0.2.1", DeviceType.Router, Vendor.MikroTik, model: "RB5009", firmwareVersion: "7.16");
        device.UpdateDiscoveredIdentity("192.0.2.1", DeviceType.Unknown, Vendor.Generic, null, null, "", null);
        Assert.Equal("router-1", device.Hostname);
        Assert.Equal("RB5009", device.Model);
        Assert.Equal("7.16", device.FirmwareVersion);
    }

    [Fact]
    public void Existing_interface_refreshes_up_to_down_and_marks_seen()
    {
        var iface = new DeviceInterface(DeviceId.New(), 7, "ether1", adminStatus: InterfaceAdminStatus.Up, operStatus: InterfaceOperStatus.Up);
        var seen = DateTime.UtcNow.AddMinutes(1);
        iface.Refresh("ether1", "WAN", null, null, InterfaceAdminStatus.Up, InterfaceOperStatus.Down, 1_000_000_000, "ethernet", seen);
        Assert.Equal(InterfaceOperStatus.Down, iface.OperStatus);
        Assert.Equal(seen, iface.LastSeenAtUtc);
    }

    [Fact]
    public void Missing_interface_is_retained_but_marked_unknown()
    {
        var iface = new DeviceInterface(DeviceId.New(), 7, "ether1", operStatus: InterfaceOperStatus.Up);
        iface.MarkStale();
        Assert.Equal(InterfaceOperStatus.Unknown, iface.OperStatus);
    }

    [Fact]
    public void Rediscovered_link_is_unstaled_and_accepts_stronger_evidence()
    {
        var link = new NetworkLink(InterfaceId.New(), InterfaceId.New(), LinkType.Unknown, DiscoverySource.Imported, 0.4);
        link.MarkStale();
        link.RefreshEvidence(LinkType.Physical, DiscoverySource.Lldp, 0.95);
        Assert.False(link.IsStale);
        Assert.True(link.IsConfirmed);
        Assert.Equal(0.95, link.Confidence);
        Assert.Equal(DiscoverySource.Lldp, link.DiscoverySource);
    }

    [Fact]
    public void Discovered_link_starts_with_unknown_operational_state()
    {
        var link = new NetworkLink(InterfaceId.New(), InterfaceId.New(), LinkType.Physical, DiscoverySource.Lldp, 0.95);
        Assert.Equal(InterfaceAdminStatus.Unknown, link.AdminStatus);
        Assert.Equal(InterfaceOperStatus.Unknown, link.OperStatus);
        Assert.True(link.IsConfirmed);
    }

    [Fact]
    public void Unknown_evidence_never_auto_confirms_link()
    {
        var link = new NetworkLink(InterfaceId.New(), InterfaceId.New(), LinkType.Unknown, DiscoverySource.Unknown, 1.0);
        Assert.False(link.IsConfirmed);
    }
}
