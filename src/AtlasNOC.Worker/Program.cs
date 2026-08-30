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

    services.AddInfrastructure(context.Configuration.GetValue<bool>("LabMode"));
});

var host = builder.Build();
await host.RunAsync();