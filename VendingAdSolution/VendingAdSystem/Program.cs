using Microsoft.AspNetCore.Authentication.Cookies;
using VendingAdSystem.Infrastructure;
using VendingAdSystem.Infrastructure.Health;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prometheus;
using Serilog;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Metrics;
using VendingAdSystem.Middleware;

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting VendingAd CMS application");

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog();

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IApplicationMetrics, PrometheusApplicationMetrics>();
builder.Services.AddHostedService<ActiveDeviceMetricsCollector>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
    };
});

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Startup database initialization strategy.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var applyMigrationsOnStartup = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
    var ensureCreatedOnStartup = builder.Configuration.GetValue<bool>("Database:EnsureCreatedOnStartup");
    var resetOnStartup = builder.Configuration.GetValue<bool>("Database:ResetOnStartup");
    var resetSchemaOnStartup = builder.Configuration.GetValue<bool>("Database:ResetSchemaOnStartup");
    var seedDemoData = builder.Configuration.GetValue<bool>("Seed:EnableDemoData");
    var allowDemoDataOutsideDevelopment = builder.Configuration.GetValue<bool>("Seed:AllowDemoDataOutsideDevelopment");

    if (seedDemoData && !app.Environment.IsDevelopment())
    {
        if (!allowDemoDataOutsideDevelopment)
            throw new InvalidOperationException("Seed:EnableDemoData must be false outside Development unless Seed:AllowDemoDataOutsideDevelopment=true.");

        app.Logger.LogWarning("Demo seed data is enabled outside Development.");
    }

    if (resetOnStartup)
    {
        if (!app.Environment.IsDevelopment())
            app.Logger.LogWarning("Database:ResetOnStartup is enabled outside Development. The configured database will be deleted and recreated.");

        if (!applyMigrationsOnStartup && !ensureCreatedOnStartup)
            throw new InvalidOperationException("Database:ResetOnStartup requires Database:ApplyMigrationsOnStartup=true or Database:EnsureCreatedOnStartup=true.");

        db.Database.EnsureDeleted();
    }

    if (resetSchemaOnStartup && !resetOnStartup)
    {
        if (!app.Environment.IsDevelopment())
            app.Logger.LogWarning("Database:ResetSchemaOnStartup is enabled. All tables will be dropped and recreated via migrations.");

        await DropAllTablesAsync(db);

        if (applyMigrationsOnStartup)
        {
            db.Database.Migrate();
        }
        else if (ensureCreatedOnStartup)
        {
            db.Database.EnsureCreated();
        }
    }
    else if (applyMigrationsOnStartup)
    {
        if (db.Database.IsRelational())
        {
            try
            {
                db.Database.Migrate();
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
            {
                app.Logger.LogWarning("Tables already exist but migration history is out of sync. Dropping and recreating schema.");
                await DropAllTablesAsync(db);
                db.Database.Migrate();
            }
        }
        else if (ensureCreatedOnStartup)
        {
            db.Database.EnsureCreated();
        }
    }
    else if (ensureCreatedOnStartup)
    {
        db.Database.EnsureCreated();
    }

    if (seedDemoData)
        DatabaseSeeder.Seed(db);
}

app.UseStaticFiles();

var uploadsPath = builder.Configuration["UploadsPath"];
if (!string.IsNullOrWhiteSpace(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
}
app.UseRouting();
app.UseHttpMetrics(options =>
{
    options.ReduceStatusCodeCardinality();
});
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task DropAllTablesAsync(AppDbContext db)
{
    var connection = db.Database.GetDbConnection() as NpgsqlConnection;
    if (connection == null) return;

    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose) await connection.OpenAsync();

    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DO $$ DECLARE
                r RECORD;
            BEGIN
                FOR r IN (
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public'
                ) LOOP
                    EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;";
        await cmd.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose) await connection.CloseAsync();
    }
}

public partial class Program { }
