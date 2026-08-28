using System;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public class DeviceValidator : IDeviceValidator
{
    public ValidationResult Validate(Device device)
    {
        var result = ValidationResult.Success();
        if (string.IsNullOrWhiteSpace(device.Name))
            result.AddError("Device name is required");
        if (string.IsNullOrWhiteSpace(device.IpAddress))
            result.AddError("IP address is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(device.IpAddress,
                @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}" +
                @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            result.AddError($"Invalid IP address format: {device.IpAddress}");
        if (string.IsNullOrWhiteSpace(device.CreatedBy))
            result.AddError("CreatedBy is required");
        return result;
    }
}
