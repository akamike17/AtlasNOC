using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class WorkerRegistrationTests
{
    private static readonly Type[] NocWorkerTypes =
    {
        typeof(PollingWorker),
        typeof(DiscoveryWorker),
        typeof(TopologyCorrelationWorker),
        typeof(MetricRetentionWorker),
        typeof(AlertEvaluationWorker),
        typeof(NotificationWorker)
    };

    [Fact]
    public void AddInfrastructure_DoesNotRegisterNocHostedServices()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        var hostedTypes = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .Where(t => t is not null)
            .ToList();
        Assert.DoesNotContain(hostedTypes, t => NocWorkerTypes.Contains(t));
    }

    [Fact]
    public void AddAtlasWorkers_RegistersEveryNocWorkerExactlyOnce()
    {
        var services = new ServiceCollection();

        services.AddAtlasWorkers();

        var hostedTypes = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();
        foreach (var workerType in NocWorkerTypes)
            Assert.Equal(1, hostedTypes.Count(t => t == workerType));
    }
}
