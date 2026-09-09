using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Security.Cryptography.X509Certificates;

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

    // Data Protection persistido en MySQL (necesario para descifrar credenciales)
    var dataProtection = services.AddDataProtection()
        .SetApplicationName("AtlasNOC")
        .PersistKeysToDbContext<AtlasNOCDbContext>();

    var keyRingCertThumbprint = context.Configuration["DataProtection:KeyRingCertThumbprint"];
    if (!string.IsNullOrWhiteSpace(keyRingCertThumbprint))
    {
        using var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        certStore.Open(OpenFlags.ReadOnly);
        var cert = certStore.Certificates
            .Find(X509FindType.FindByThumbprint, keyRingCertThumbprint, validOnly: false)
            .OfType<X509Certificate2>()
            .FirstOrDefault();
        if (cert is not null)
            dataProtection.ProtectKeysWithCertificate(cert);
        else if (!context.HostingEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException($"Certificado de Data Protection con thumbprint '{keyRingCertThumbprint}' no encontrado en el almacén. En producción esto es obligatorio.");
        }
    }

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
