using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// AtlasNOC.Worker: procesamiento en segundo plano (discovery, polling, correlación, alertas, notificaciones, retention).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AtlasNOCDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.Parse("8.0.36-mysql")));

builder.Services.AddInfrastructure();

var host = builder.Build();
await host.RunAsync();