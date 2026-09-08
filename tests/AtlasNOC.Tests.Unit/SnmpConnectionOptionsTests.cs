using AtlasNOC.Application.Probes;
using AtlasNOC.Domain.Enums;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class SnmpConnectionOptionsTests
{
    [Fact]
    public void V3_no_auth_no_priv_requires_only_user()
        => new SnmpConnectionOptions(SnmpVersion.V3, null, "monitor", null, null, null, null).Validate();

    [Fact]
    public void V3_rejects_privacy_without_authentication()
    {
        var options = new SnmpConnectionOptions(SnmpVersion.V3, null, "monitor", null, null, "AES", "private-pass");
        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void V3_accepts_sha256_and_aes_auth_priv()
        => new SnmpConnectionOptions(SnmpVersion.V3, null, "monitor", "SHA256", "auth-pass", "AES", "private-pass").Validate();

    [Fact]
    public void V3_rejects_unsupported_protocols()
    {
        var options = new SnmpConnectionOptions(SnmpVersion.V3, null, "monitor", "MD5", "auth-pass", null, null);
        Assert.Throws<ArgumentException>(options.Validate);
    }
}
