namespace AtlasNOC.Application.Services;

public sealed record LocalNetworkProfileDto(
    string InterfaceName,
    string Address,
    string SuggestedScope,
    string? Gateway,
    bool IsUp);

public interface ILocalNetworkProfileService
{
    IReadOnlyList<LocalNetworkProfileDto> GetProfiles();
}
