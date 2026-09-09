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
/// Fase 9 / especificación §4: verifica el lockout de login real.
///  - 5 contraseñas incorrectas -> cuenta bloqueada (locked out);
///  - contraseña correcta durante el lockout -> sigue siendo rechazada;
///  - tras expirar la ventana de bloqueo -> puede entrar de nuevo.
///
/// Se invoca directamente la acción Login del AccountController usando los
/// servicios reales de Identity sobre una base de test dedicada, igual que
/// <see cref="LoginIsActiveTests"/>, para no depender de Playwright.
/// </summary>
public class LoginLockoutTests : IAsyncLifetime
{
    private readonly LockoutTestFactory _factory = new LockoutTestFactory();

    private const string UserName = "lockout-user";
    private const string CorrectPassword = "Password123!";
    private const string WrongPassword = "WrongPassword123!";

    private void SkipIfNoDb()
    {
        if (_factory.IsSkipped(out var reason)) throw new SkipTestException(reason);
    }

    public async Task InitializeAsync()
    {
        SkipIfNoDb();

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roles.RoleExistsAsync(ApplicationRole.NocOperator))
            await roles.CreateAsync(new ApplicationRole(ApplicationRole.NocOperator));

        if (await users.FindByNameAsync(UserName) is null)
        {
            var u = new ApplicationUser(UserName)
            {
                Email = "lockout@x.com",
                EmailConfirmed = true,
                IsActive = true,
            };
            await users.CreateAsync(u, CorrectPassword);
            await users.AddToRoleAsync(u, ApplicationRole.NocOperator);
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory.IsSkipped(out _))
        {
            await _factory.DisposeAsync();
            return;
        }

        // Asegura que el usuario quede usable entre tests de la misma colección
        // (reset de lockout). De lo contrario un test contaminaría al siguiente.
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByNameAsync(UserName);
        if (u is not null && await users.IsLockedOutAsync(u))
            await users.SetLockoutEndDateAsync(u, null);

        await _factory.DisposeAsync();
    }

    private static IActionResult LoginAs(IServiceProvider sp, string userName, string password)
    {
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

    [SkippableFact]
    public async Task Five_wrong_passwords_lock_out_the_account()
    {
        SkipIfNoDb();
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByNameAsync(UserName);
        await users.SetLockoutEndDateAsync(u!, null); // estado limpio

        for (var i = 0; i < 5; i++)
        {
            var r = LoginAs(scope.ServiceProvider, UserName, WrongPassword);
            Assert.IsType<ViewResult>(r); // nunca éxito
        }

        Assert.True(await users.IsLockedOutAsync(u!),
            "Tras 5 intentos fallidos la cuenta debe quedar bloqueada.");
    }

    [SkippableFact]
    public async Task Correct_password_during_lockout_is_still_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByNameAsync(UserName);
        await users.SetLockoutEndDateAsync(u!, null);

        for (var i = 0; i < 5; i++)
            LoginAs(scope.ServiceProvider, UserName, WrongPassword);

        Assert.True(await users.IsLockedOutAsync(u!));

        // La contraseña correcta durante el bloqueo no debe entrar.
        var result = LoginAs(scope.ServiceProvider, UserName, CorrectPassword);
        Assert.IsType<ViewResult>(result);
        Assert.True(await users.IsLockedOutAsync(u!));
    }

    [SkippableFact]
    public async Task After_lockout_window_expires_login_succeeds()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByNameAsync(UserName);
        await users.SetLockoutEndDateAsync(u!, null);

        for (var i = 0; i < 5; i++)
            LoginAs(scope.ServiceProvider, UserName, WrongPassword);

        Assert.True(await users.IsLockedOutAsync(u!));

        // Simula la expiración de la ventana (5 min): fija el fin del bloqueo en el pasado.
        await users.SetLockoutEndDateAsync(u!, DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(await users.IsLockedOutAsync(u!));

        var result = LoginAs(scope.ServiceProvider, UserName, CorrectPassword);
        Assert.IsType<RedirectToActionResult>(result);
    }
}

public class LockoutTestFactory : WebApplicationFactory<Program>
{
    private string _connectionString = null!;
    private string? _skipReason;

    public LockoutTestFactory()
    {
        var resolved = TestDatabaseConfiguration.TryResolve();
        if (resolved is null)
        {
            _skipReason =
                $"Omitting E2E lockout tests: '{TestDatabaseConfiguration.EnvironmentVariableName}' " +
                "environment variable is not set.";
            return;
        }

        _connectionString = TestDatabaseConfiguration.WithDatabaseSuffix(resolved, "_lockout");
    }

    /// <summary><c>true</c> si el fixture debe omitirse por falta de base de test.</summary>
    public bool IsSkipped(out string reason)
    {
        reason = _skipReason ?? string.Empty;
        return _skipReason is not null;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (IsSkipped(out _)) return;

        builder.UseSetting("LabMode", "true");
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AtlasNOCDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AtlasNOCDbContext>(o =>
                o.UseMySql(_connectionString, ServerVersion.Parse("8.0.36-mysql")));
        });
    }
}