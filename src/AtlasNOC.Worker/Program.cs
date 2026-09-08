using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// AtlasNOC.Worker: procesamiento en segundo plano (discovery, polling, correlación, alertas, notificaciones, retention).
var builder = Host.CreateDefaultBuilder(args);

builder.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.ConfigureServices((context, services) =>
{
    services.AddDbContext<AtlasNOCDbContext>(options =>
        options.UseMySql(
            context.Configuration.GetConnectionString("DefaultConnection"),
            ServerVersion.Parse("8.0.36-mysql")));

    services.AddSingleton(context.Configuration.GetSection("Ubiquiti").Get<AtlasNOC.Infrastructure.Devices.UbiquitiOptions>()
        ?? new AtlasNOC.Infrastructure.Devices.UbiquitiOptions());
    services.Configure<AtlasNOC.Infrastructure.Services.DiscoveryOptions>(
        context.Configuration.GetSection("Discovery"));
    services.Configure<AtlasNOC.Infrastructure.Services.PollingOptions>(
        context.Configuration.GetSection("Polling"));
    services.Configure<AtlasNOC.Infrastructure.Services.NotificationOptions>(
        context.Configuration.GetSection("Notifications"));
    services.AddInfrastructure(context.Configuration.GetValue<bool>("LabMode"));
    services.AddAtlasWorkers();
});

var host = builder.Build();
await host.RunAsync();
