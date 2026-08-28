using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IDeviceValidator
{
    ValidationResult Validate(Device device);
}
