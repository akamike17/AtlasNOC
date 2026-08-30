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

        // Security
        services.AddScoped<ICredentialProtector, CredentialProtector>();

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
        }).AddStandardResilienceHandler();

        services.AddHttpClient("ubiquiti", (sp, c) =>
        {
            var opts = sp.GetRequiredService<UbiquitiOptions>();
            c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 10);
        }).AddStandardResilienceHandler();

        services.AddSingleton(new MikroTikOptions());
        services.AddSingleton(new UbiquitiOptions());
        // Orden = especificidad. Simulated primero: en modo LAB toma prioridad y no
        // se dispara con fingerprints reales (solo VendorHint "simulated" o IPs 10.0.x).
        services.AddSingleton<IDeviceDriver, SimulatedNetworkDriver>();
        services.AddSingleton<IDeviceDriver, MikroTikDriver>();
        services.AddSingleton<IDeviceDriver, UbiquitiDriver>();
        services.AddSingleton<IDeviceDriver, GenericSnmpDriver>();
        services.AddSingleton<IDeviceDriverRegistry, DeviceDriverRegistry>();

        // Application services
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<ILinkService, LinkService>();
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
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<ICredentialService, CredentialService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();

        // Workers (register as hosted services where the host chooses)
        services.AddHostedService<PollingWorker>();
        services.AddHostedService<DiscoveryWorker>();
        services.AddHostedService<TopologyCorrelationWorker>();
        services.AddHostedService<MetricRetentionWorker>();
        services.AddHostedService<AlertEvaluationWorker>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }
}