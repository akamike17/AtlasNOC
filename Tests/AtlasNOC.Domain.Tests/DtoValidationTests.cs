using System.ComponentModel.DataAnnotations;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Web.Models;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class DtoValidationTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.0.2.10")]
    [InlineData("255.255.255.255")]
    public void CreateDeviceRequest_AcceptsValidIpv4(string address)
    {
        var request = new CreateDeviceRequest
        {
            Name = "router-01",
            IpAddress = address,
            Type = DeviceType.Router
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("999.999.1.1")]
    [InlineData("192.0.2")]
    [InlineData("not-an-ip")]
    public void CreateDeviceRequest_RejectsInvalidIpv4(string address)
    {
        var request = new CreateDeviceRequest
        {
            Name = "router-01",
            IpAddress = address,
            Type = DeviceType.Router
        };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(CreateDeviceRequest.IpAddress)));
    }

    private static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
