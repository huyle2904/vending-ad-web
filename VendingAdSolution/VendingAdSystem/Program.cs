using Microsoft.AspNetCore.Authentication.Cookies;
using VendingAdSystem.Infrastructure;
using VendingAdSystem.Infrastructure.Health;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
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

    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    var laMoiTruongPhatTrien = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
            options.AccessDeniedPath = "/account/login";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = laMoiTruongPhatTrien
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
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

    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'; " +
            "img-src 'self' data: blob:; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "font-src 'self' data:; " +
            "connect-src 'self';";

        await next();
    });

    // Database initialization
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applyMigrationsOnStartup = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
        var ensureCreatedOnStartup = builder.Configuration.GetValue<bool>("Database:EnsureCreatedOnStartup");
        var resetOnStartup = builder.Configuration.GetValue<bool>("Database:ResetOnStartup");
        var resetSchemaOnStartup = builder.Configuration.GetValue<bool>("Database:ResetSchemaOnStartup");
        var seedDemoData = builder.Configuration.GetValue<bool>("Seed:EnableDemoData");
        var allowDemoDataOutsideDevelopment = builder.Configuration.GetValue<bool>("Seed:AllowDemoDataOutsideDevelopment");

        if (applyMigrationsOnStartup && ensureCreatedOnStartup)
            throw new InvalidOperationException("Database:ApplyMigrationsOnStartup and Database:EnsureCreatedOnStartup cannot both be true.");

        if (ensureCreatedOnStartup)
            throw new InvalidOperationException("Database:EnsureCreatedOnStartup is no longer supported in the SQL Server-only runtime.");

        if (seedDemoData && !app.Environment.IsDevelopment() && !allowDemoDataOutsideDevelopment)
            throw new InvalidOperationException("Seed:EnableDemoData must be false outside Development unless Seed:AllowDemoDataOutsideDevelopment=true.");

        if ((resetOnStartup || resetSchemaOnStartup) && !app.Environment.IsDevelopment())
            throw new InvalidOperationException("Database reset flags must remain false outside Development.");

        if (resetOnStartup)
        {
            db.Database.EnsureDeleted();
        }

        if (applyMigrationsOnStartup)
            db.Database.Migrate();

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
    app.UseHttpMetrics(options => options.ReduceStatusCodeCardinality());
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

public partial class Program { }
