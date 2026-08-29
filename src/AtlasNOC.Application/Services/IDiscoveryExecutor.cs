namespace AtlasNOC.Application.Services;

/// <summary>Ejecutor del pipeline de descubrimiento (Flujo C): ICMP → SNMP → upsert → correlación → enlaces.</summary>
public interface IDiscoveryExecutor
{
    /// <summary>Ejecuta el pipeline para un DiscoveryRun ya persistido y actualiza su resumen.</summary>
    Task ExecuteAsync(Guid runId, CancellationToken ct = default);
}