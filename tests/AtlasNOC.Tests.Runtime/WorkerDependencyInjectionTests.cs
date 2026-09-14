using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AtlasNOC.Tests.Runtime;

public sealed class WorkerDependencyInjectionTests
{
    [Fact]
    public void Worker_composition_validates_all_services_without_identity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AtlasNOCDbContext>(options =>
            options.UseMySql("Server=localhost;Database=atlasnoc_integration_test;", ServerVersion.Parse("8.0.36-mysql")));
        services.AddInfrastructure(labMode: true);
        services.AddAtlasWorkers();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AtlasNOC.Domain.Identity.ApplicationUser>>());
    }
}
