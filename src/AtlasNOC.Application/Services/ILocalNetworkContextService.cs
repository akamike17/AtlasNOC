namespace AtlasNOC.Application.Services;

/// <summary>Contexto de la red local que Atlas puede proponer al operador.</summary>
public sealed record LocalNetworkContext(
    string InterfaceName,
    string LocalIp,
    string? GatewayIp,
    string SuggestedSubnetCidr);

public interface ILocalNetworkContextService
{
    IReadOnlyList<LocalNetworkContext> GetContexts();
    LocalNetworkContext? GetPreferredContext();
}
