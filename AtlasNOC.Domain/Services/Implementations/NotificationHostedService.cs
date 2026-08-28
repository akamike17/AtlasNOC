using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public sealed class NotificationHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationHostedService> _logger;

    public NotificationHostedService(IServiceProvider serviceProvider, ILogger<NotificationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification hosted service starting");

        // NotificationService doesn't need a continuous loop - it just needs to be available
        // The actual notification sending happens on-demand via INotificationService
        // This hosted service ensures the service registration is complete and available
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected
        }

        _logger.LogInformation("Notification hosted service stopped");
    }
}