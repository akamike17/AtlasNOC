using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Devices;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Persistence.Repositories;
using AtlasNOC.Infrastructure.Probes;
using AtlasNOC.Infrastructure.Security;
using AtlasNOC.Infrastructure.Services;
using AtlasNOC.Infrastructure.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtlasNOC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool labMode = false)
    {
        // Persistence
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ILinkRepository, LinkRepository>();
        services.AddScoped<IInterfaceRepository, InterfaceRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IDiscoveryRunRepository, DiscoveryRunRepository>();
        services.AddScoped<INeighborObservationRepository, NeighborObservationRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISubscriberRepository, SubscriberRepository>();
        services.AddScoped<IServiceEndpointRepository, ServiceEndpointRepository>();

        // Security
        services.AddScoped<ICredentialProtector, CredentialProtector>();
        // Necesario para que AuditService capture IP/User-Agent del actor (§25).
        // En el Worker no hay HttpContext (null es manejado); en Web sí.
        services.AddHttpContextAccessor();

        // Probes / drivers (orden = especificidad; SnmpDriver genérico va al final como fallback)
        // ─── Modo LAB: Icmp/Snmp simulados; producción: probes reales ──────
        services.AddSingleton<IcmpProbe>();
        services.AddSingleton<SnmpProbe>();
        services.AddSingleton<IIcmpProbe>(sp =>
            new SimulatedIcmpProbe(labMode, sp.GetRequiredService<IcmpProbe>()));
        services.AddSingleton<ISnmpProbe>(sp =>
            new SimulatedSnmpProbe(labMode, sp.GetRequiredService<SnmpProbe>()));

        // ─── Fase 9: resiliencia HTTP (timeout + reintentos) para drivers ───
        // Los drivers declaran su timeout en MikroTikOptions/UbiqutiOptions;
        // aquí se aplica de verdad y se añade política de reintentos.
        services.AddHttpClient("mikrotik", (sp, c) =>
        {
            var opts = sp.GetRequiredService<MikroTikOptions>();
            c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 10);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var opts = sp.GetRequiredService<MikroTikOptions>();
            var logger = sp.GetRequiredService<ILogger<MikroTikDriver>>();
            var handler = new HttpClientHandler();
            if (opts.SkipCertificateValidation || !string.IsNullOrWhiteSpace(opts.PinnedCertificateThumbprint))
            {
                if (opts.SkipCertificateValidation)
                    logger.LogWarning("La validación TLS de RouterOS está desactivada explícitamente");
                handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
                {
                    if (!string.IsNullOrWhiteSpace(opts.PinnedCertificateThumbprint))
                    {
                        var expected = opts.PinnedCertificateThumbprint.Replace(":", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
                        var actual = certificate?.GetCertHashString() ?? string.Empty;
                        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
                    }
                    return opts.SkipCertificateValidation || errors == System.Net.Security.SslPolicyErrors.None;
                };
            }
            return handler;
        }).AddStandardResilienceHandler();

        services.AddHttpClient("ubiquiti", (sp, c) =>
        {
            var opts = sp.GetRequiredService<UbiquitiOptions>();
            c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 10);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var opts = sp.GetRequiredService<UbiquitiOptions>();
            var handler = new HttpClientHandler();
            if (opts.SkipCertificateValidation || !string.IsNullOrWhiteSpace(opts.PinnedCertificateThumbprint))
            {
                handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
                {
                    if (!string.IsNullOrWhiteSpace(opts.PinnedCertificateThumbprint))
                    {
                        var expected = opts.PinnedCertificateThumbprint.Replace(":", string.Empty).Replace(" ", string.Empty);
                        return (certificate?.GetCertHashString() ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
                    }
                    return opts.SkipCertificateValidation || errors == System.Net.Security.SslPolicyErrors.None;
                };
            }
            return handler;
        }).AddStandardResilienceHandler();

        services.TryAddSingleton(new MikroTikOptions());
        services.TryAddSingleton(new UbiquitiOptions());
        // Orden = especificidad. Simulated primero: en modo LAB toma prioridad y no
        // se dispara con fingerprints reales (solo VendorHint "simulated" o IPs 10.0.x).
        services.AddSingleton<IDeviceDriver, SimulatedNetworkDriver>();
        services.AddSingleton<IDeviceDriver, MikroTikDriver>();
        services.AddSingleton<IDeviceDriver, AirOsDeviceDriver>();
        services.AddSingleton<IDeviceDriver, UbiquitiDriver>();
        services.AddSingleton<IDeviceDriver, GenericSnmpDriver>();
        services.AddSingleton<IDeviceDriverRegistry, DeviceDriverRegistry>();

        // Application services
        services.AddOptions<DiscoveryOptions>();
        services.AddOptions<PollingOptions>();
        services.AddOptions<NotificationOptions>();
        services.AddHttpClient("notifications", (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.WebhookTimeoutSeconds));
        });
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<ILinkService, LinkService>();
        services.AddScoped<IInterfaceService, InterfaceService>();
        services.AddScoped<ISubscriberService, SubscriberService>();
        services.AddScoped<IServiceEndpointService, ServiceEndpointService>();
        services.AddScoped<ITopologyService, TopologyService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddScoped<IDiscoveryExecutor, DiscoveryExecutor>();
        services.AddScoped<INetworkFingerprintService, NetworkFingerprintService>();
        services.AddScoped<ITopologyCorrelationEngine, TopologyCorrelationEngine>();
        services.AddScoped<IPollingService, PollingService>();
        services.AddScoped<IMetricWriter, MetricWriter>();
        services.AddScoped<IMetricQueryService, MetricQueryService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IAlertEvaluationEngine, AlertEvaluationEngine>();
        services.AddScoped<IIncidentCorrelationEngine, IncidentCorrelationEngine>();
        services.AddScoped<IAlertRuleService, AlertRuleService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<ICredentialService, CredentialService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();

        return services;
    }

    /// <summary>
    /// Registra los procesos NOC en segundo plano. El host Worker debe invocarlo
    /// explícitamente; el host Web permanece exclusivamente HTTP por defecto.
    /// </summary>
    public static IServiceCollection AddAtlasWorkers(this IServiceCollection services)
    {
        services.AddHostedService<PollingWorker>();
        services.AddHostedService<DiscoveryWorker>();
        services.AddHostedService<TopologyCorrelationWorker>();
        services.AddHostedService<MetricRetentionWorker>();
        services.AddHostedService<AlertEvaluationWorker>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }
}
