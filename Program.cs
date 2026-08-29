using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using System.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog Configuration ─────────────────────────────────────────────────────
var logLevel = builder.Configuration.GetValue<string>("Serilog:MinimumLevel:Default") ?? "Information";
var logOutputTemplate = builder.Configuration.GetValue<string>("Serilog:OutputTemplate")
    ?? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(new Serilog.Core.LoggingLevelSwitch(GetLogEventLevel(logLevel)))
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("AtlasNOC", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: logOutputTemplate)
    .WriteTo.File(
        path: builder.Configuration["Serilog:WriteTo:File:Path"] ?? "logs/atlasnoc-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: logOutputTemplate,
        retainedFileCountLimit: 30)
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Serilog:WriteTo:Seq:ServerUrl"] ?? "http://localhost:5341",
        apiKey: builder.Configuration["Serilog:WriteTo:Seq:ApiKey"])
    .CreateLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// ─── Configuration ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. " +
        "Add it to appsettings.json, environment variables, or user secrets.");
}

// ─── DbContext (MySQL via Pomelo) ──────────────────────────────────────────────
builder.Services.AddDbContext<AtlasNOCDbContext>(options =>
{
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
    options.UseMySql(connectionString, serverVersion, mysql =>
    {
        mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        mysql.MigrationsAssembly("AtlasNOC.Domain");
        mysql.CommandTimeout(30);
    });
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableDetailedErrors(builder.Environment.IsDevelopment());
}, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

// ─── Hybrid Cache (L1 Memory + L2 Redis) ──────────────────────────────────────
var redisConnection = builder.Configuration.GetConnectionString("Redis");

if (string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "AtlasNOC:";
    });
}

// ─── Data Protection ────────────────────────────────────────────────────────────
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("AtlasNOC")
    .PersistKeysToDbContext<AtlasNOCDbContext>();
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}
else if (!builder.Environment.IsDevelopment())
{
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var certificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
    if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(certificatePassword))
    {
        throw new InvalidOperationException(
            "Production on non-Windows hosts requires DataProtection:CertificatePath and DataProtection:CertificatePassword so persisted keys are encrypted at rest.");
    }

    var certificate = new X509Certificate2(
        certificatePath,
        certificatePassword,
        X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    if (!certificate.HasPrivateKey)
    {
        certificate.Dispose();
        throw new InvalidOperationException("The configured Data Protection certificate does not contain a private key.");
    }

    dataProtection.ProtectKeysWithCertificate(certificate);
}

// ─── HTTP Client for CVE Fetcher ──────────────────────────────────────────────
builder.Services.AddHttpClient("NvdCve", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

// ─── HTTP Client for Notification Service ───────────────────────────────────────
builder.Services.AddHttpClient<INotificationService, NotificationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ─── Controllers with Versioning ───────────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesAttribute("application/json"));
})
.AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
    opts.JsonSerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    opts.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
});

// ─── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-API-Version"),
        new QueryStringApiVersionReader("api-version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ─── Swagger/OpenAPI ───────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication. Example: \"X-API-Key: your-a...e\""
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.EnableAnnotations();
    options.UseAllOfToExtendReferenceSchemas();
});

// ─── Health Checks ─────────────────────────────────────────────────────────────
var healthChecks = builder.Services.AddHealthChecks()
    .AddDbContextCheck<AtlasNOCDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "mysql", "ready" })
    .AddMySql(
        connectionString,
        name: "mysql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "mysql", "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy("Self check passed"), tags: new[] { "live" });

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(
        redisConnection,
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "cache", "redis", "ready" });
}

// ─── OpenTelemetry ─────────────────────────────────────────────────────────────
var otelEndpointValue = builder.Configuration["OpenTelemetry:Endpoint"]
    ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
Uri? otelEndpoint = null;
if (!string.IsNullOrWhiteSpace(otelEndpointValue)
    && (!Uri.TryCreate(otelEndpointValue, UriKind.Absolute, out otelEndpoint)
        || (otelEndpoint.Scheme != Uri.UriSchemeHttp && otelEndpoint.Scheme != Uri.UriSchemeHttps)))
{
    throw new InvalidOperationException("OpenTelemetry endpoint must be an absolute HTTP or HTTPS URI.");
}

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]
    ?? Assembly.GetExecutingAssembly().GetName().Name
    ?? "AtlasNOC";

var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.namespace"] = "AtlasNOC"
        }));

openTelemetry.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("AtlasNOC");
    if (otelEndpoint is not null)
    {
        tracing.AddOtlpExporter(options => options.Endpoint = otelEndpoint);
    }
});

openTelemetry.WithMetrics(metrics =>
{
    metrics.AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
    if (otelEndpoint is not null)
    {
        metrics.AddOtlpExporter(options => options.Endpoint = otelEndpoint);
    }
});

if (otelEndpoint is not null)
{
    openTelemetry.WithLogging(logging =>
        logging.AddOtlpExporter(options => options.Endpoint = otelEndpoint));
}

// ─── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRepository<Device>, EfCoreRepository<Device>>();
builder.Services.AddScoped<IRepository<Credential>, EfCoreRepository<Credential>>();
builder.Services.AddScoped<IRepository<Alert>, EfCoreRepository<Alert>>();
builder.Services.AddScoped<IRepository<Incident>, EfCoreRepository<Incident>>();
builder.Services.AddScoped<IRepository<AuditEvent>, EfCoreRepository<AuditEvent>>();
builder.Services.AddScoped<IRepository<ApiKey>, EfCoreRepository<ApiKey>>();
builder.Services.AddScoped<IRepository<CveRecord>, EfCoreRepository<CveRecord>>();
builder.Services.AddScoped<IRepository<AtlasNOC.Domain.Entities.NotificationChannel>, EfCoreRepository<AtlasNOC.Domain.Entities.NotificationChannel>>();
builder.Services.AddScoped<IRepository<DiscoveryRun>, EfCoreRepository<DiscoveryRun>>();

// ─── Domain Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ICredentialService, CredentialService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<ICveService, CveService>();
builder.Services.AddScoped<ISnmpService, SnmpService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<ITopologyService, TopologyService>();
builder.Services.AddScoped<IPollingService, PollingService>();
builder.Services.AddScoped<IMetricHistoryService, MetricHistoryService>();
builder.Services.AddScoped<IMonitoringService, MonitoringService>();
builder.Services.AddScoped<IDeviceValidator, DeviceValidator>();
builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();
builder.Services.AddScoped<ApiKeyStore>();
builder.Services.AddOptions<PollingOptions>()
    .BindConfiguration("Polling")
    .Validate(options => options.IntervalSeconds is >= 5 and <= 86400,
        "Polling:IntervalSeconds must be between 5 and 86400.")
    .Validate(options => options.MaxConcurrency is >= 1 and <= 256,
        "Polling:MaxConcurrency must be between 1 and 256.")
    .Validate(options => options.SnmpTimeout > TimeSpan.Zero && options.SnmpTimeout <= TimeSpan.FromMinutes(2),
        "Polling:SnmpTimeout must be greater than zero and no more than two minutes.")
    .Validate(options => options.RetentionDays is >= 1 and <= 3650,
        "Polling:RetentionDays must be between 1 and 3650.")
    .ValidateOnStart();

// ─── Hosted Services ──────────────────────────────────────────────────────────────
builder.Services.AddHostedService<PollingHostedService>();
builder.Services.AddHostedService<NotificationHostedService>();
builder.Services.AddHostedService<CveBackgroundService>();

// ─── Authentication & Authorization ────────────────────────────────────────────
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator"));
    options.AddPolicy("OperatorOrAdmin", policy => policy.RequireRole("Administrator", "NocOperator"));
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("Administrator", "NocOperator", "ReadOnly"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too many requests",
            status = StatusCodes.Status429TooManyRequests,
            traceId = context.HttpContext.TraceIdentifier
        }, cancellationToken);
    };
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ─── CORS ──────────────────────────────────────────────────────────────────────
var productionAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? Array.Empty<string>();

if (!builder.Environment.IsDevelopment() && productionAllowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain at least one explicit origin outside Development.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(productionAllowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });

    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

var app = builder.Build();

// ─── Database Migrations ───────────────────────────────────────────────────────
if (builder.Configuration.GetValue<bool>("AtlasNoc:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations.");
        throw;
    }
}

// ─── Global Error Handling Middleware ──────────────────────────────────────────
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

        logger.LogError(ex, "Unhandled exception: {Message} TraceId: {TraceId}", ex.Message, traceId);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "Internal server error",
            traceId = traceId,
            detail = app.Environment.IsDevelopment() ? ex.Message : "An unexpected error occurred. Please contact support with the trace ID."
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

// ─── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

if (builder.Configuration.GetValue("AtlasNoc:RequireHttps", true))
{
    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseHttpsRedirection();
}

// ─── One-time administrator bootstrap ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var apiKeyStore = scope.ServiceProvider.GetRequiredService<ApiKeyStore>();
    var activeKeys = await apiKeyStore.ListActiveKeysAsync();
    if (activeKeys.Count == 0)
    {
        var bootstrapKey = builder.Configuration["AtlasNoc:BootstrapAdminApiKey"];
        if (string.IsNullOrWhiteSpace(bootstrapKey))
        {
            throw new InvalidOperationException(
                "No active API keys exist. Configure AtlasNoc:BootstrapAdminApiKey with at least 32 characters for the first startup.");
        }

        var owner = builder.Configuration["AtlasNoc:BootstrapAdminOwner"] ?? "bootstrap-admin";
        if (await apiKeyStore.BootstrapAdministratorAsync(bootstrapKey, owner))
        {
            app.Logger.LogWarning(
                "Initial administrator API key created for {Owner}. Remove AtlasNoc:BootstrapAdminApiKey from the environment and rotate the key after first login.",
                owner);
        }
    }
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null ? Serilog.Events.LogEventLevel.Error :
        httpContext.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error :
        httpContext.Response.StatusCode >= 400 ? Serilog.Events.LogEventLevel.Warning :
        Serilog.Events.LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value!);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
    };
});

app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ─── Swagger UI ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("AtlasNoc:EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                $"AtlasNOC API {description.GroupName.ToUpperInvariant()}");
        }
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "AtlasNOC API Documentation";
        options.DefaultModelsExpandDepth(-1);
        options.DisplayRequestDuration();
    });
}

// ─── Health Checks ─────────────────────────────────────────────────────────────
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

// ─── Controllers ───────────────────────────────────────────────────────────────
app.MapControllers().RequireRateLimiting("api");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── Run ───────────────────────────────────────────────────────────────────────
app.Run();

// ─── Helper Methods ────────────────────────────────────────────────────────────
static Serilog.Events.LogEventLevel GetLogEventLevel(string level) => level switch
{
    "Verbose" => Serilog.Events.LogEventLevel.Verbose,
    "Debug" => Serilog.Events.LogEventLevel.Debug,
    "Information" => Serilog.Events.LogEventLevel.Information,
    "Warning" => Serilog.Events.LogEventLevel.Warning,
    "Error" => Serilog.Events.LogEventLevel.Error,
    "Fatal" => Serilog.Events.LogEventLevel.Fatal,
    _ => Serilog.Events.LogEventLevel.Information
};

static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var response = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.ToString("c"),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.ToString("c"),
            tags = entry.Value.Tags,
            data = entry.Value.Data
        }),
        timestamp = DateTimeOffset.UtcNow
    };

    await context.Response.WriteAsJsonAsync(response);
}

public partial class Program { }
