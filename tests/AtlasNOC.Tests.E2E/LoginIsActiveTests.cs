using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtlasNOC.Tests.E2E;

/// <summary>
/// Fase A4: verifica que un usuario con <c>IsActive=false</c> no puede iniciar
/// sesión (login rechazado con error genérico, sin revelar la existencia) y que
/// un usuario activo sí puede hacerlo. Se invoca directamente la acción Login
/// del AccountController usando los servicios reales de Identity sobre una base
/// de test dedicada (sin depender de Playwright ni tokens anti-forgery).
/// </summary>
public class LoginIsActiveTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new LoginTestFactory();

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var r in new[] { ApplicationRole.Administrator, ApplicationRole.NocOperator, ApplicationRole.ReadOnly })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new ApplicationRole(r));

        if (await users.FindByNameAsync("inactive-user") is null)
        {
            var u = new ApplicationUser("inactive-user") { Email = "inactive@x.com", EmailConfirmed = true, IsActive = false };
            await users.CreateAsync(u, "Password123!");
            await users.AddToRoleAsync(u, ApplicationRole.NocOperator);
        }

        if (await users.FindByNameAsync("active-user") is null)
        {
            var a = new ApplicationUser("active-user") { Email = "active@x.com", EmailConfirmed = true, IsActive = true };
            await users.CreateAsync(a, "Password123!");
            await users.AddToRoleAsync(a, ApplicationRole.NocOperator);
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static IActionResult LoginAs(IServiceProvider sp, string userName, string password)
    {
        // Vincula un HttpContext real al IHttpContextAccessor para que SignInManager
        // pueda firmar la cookie en el caso de éxito.
        var httpContext = new DefaultHttpContext { RequestServices = sp };
        sp.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var signIn = sp.GetRequiredService<SignInManager<ApplicationUser>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        var controller = new AtlasNOC.Web.Controllers.AccountController(
            signIn, userMgr,
            sp.GetRequiredService<ISetupService>(),
            sp.GetRequiredService<IAuditService>())
        {
            ControllerContext = new ControllerContext(actionContext),
            TempData = new TempDataDictionary(httpContext, sp.GetRequiredService<ITempDataProvider>()),
            Url = sp.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(actionContext),
        };
        return controller.Login(userName, password, false).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Inactive_user_is_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = LoginAs(scope.ServiceProvider, "inactive-user", "Password123!");

        // No redirige a Dashboard (sería éxito), sino que vuelve a la vista de login.
        Assert.IsNotType<RedirectToActionResult>(result);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Active_user_succeeds()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = LoginAs(scope.ServiceProvider, "active-user", "Password123!");

        Assert.IsType<RedirectToActionResult>(result);
    }
}

public class LoginTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = TestDatabaseConfiguration.Resolve();
        builder.UseSetting("LabMode", "true");
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AtlasNOCDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AtlasNOCDbContext>(o =>
                o.UseMySql(connectionString, ServerVersion.Parse("8.0.36-mysql")));
        });
    }
}