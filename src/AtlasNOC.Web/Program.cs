using AtlasNOC.Domain.Identity;
using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging estructurado (Serilog) ────────────────────────────────────────
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ─── MVC + Razor ──────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── Identity (login humano: usuario + contraseña; nunca API key) ─────────
builder.Services.AddDbContext<AtlasNOCDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.Parse("8.0.36-mysql")));

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
        // ─── Fase 9: config prudente de lockout ───────────────
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AtlasNOCDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/accessdenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    // ─── Fase 9: cookie hardening ────────────────────────────
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
});

// ─── Data Protection persistido en MySQL (necesario para cifrar credenciales) ─
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AtlasNOCDbContext>();

// ─── Fase 9: Rate limiting (anti fuerza bruta / anti abuso de API) ────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain";
        await ctx.HttpContext.Response.WriteAsync(
            "Demasiadas solicitudes. Intenta de nuevo más tarde.");
    };

    // Login humano: limita intentos por IP.
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // Setup inicial: sólo existe cuando no hay admin.
    options.AddPolicy("setup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

    // API: por API key (o IP como fallback anónimo).
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name
                ?? httpContext.Request.Headers["X-Api-Key"].ToString()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// ─── Fase 9: Health checks (liveness + readiness con DB) ──────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AtlasNOCDbContext>("database", tags: new[] { "ready" });

// ─── Infraestructura (repositorios, servicios, workers, drivers) ───────────
builder.Services.AddInfrastructure();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ─── Fase 9: cabeceras de seguridad (CSP, nosniff, frame, referrer) ───────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";
    // CSP: permite self, charts.js/cytoscape desde CDN, y data: para imágenes.
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data:; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// ─── Fase 9: endpoints de salud ───────────────────────────────────────────
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false // ninguna comprobación: sólo proceso vivo
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

// Asegura el esquema de base de datos al arrancar (migraciones controladas).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
    db.Database.EnsureCreated();
}

app.Run();