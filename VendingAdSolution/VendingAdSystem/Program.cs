using Microsoft.AspNetCore.Authentication.Cookies;
using VendingAdSystem.Infrastructure;
using VendingAdSystem.Infrastructure.Health;
using VendingAdSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Metrics;
using VendingAdSystem.Middleware;
using VendingAdSystem.Domain.Entities;

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
    builder.Services.AddSignalR();
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
            "img-src 'self' data: blob: https://img.youtube.com https://i.ytimg.com; " +
            "media-src 'self' blob:; " +
            "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; " +
            "style-src 'self' 'unsafe-inline'; " +
            "font-src 'self' data:; " +
            "connect-src 'self' ws: wss:;";

        await next();
    });

    // Database initialization. Production deployments should invoke
    // `dotnet VendingAdSystem.dll --migrate` as a separate pre-deploy step.
    var migrateOnly = args.Any(argument =>
        argument.Equals("--migrate", StringComparison.OrdinalIgnoreCase));
    var bootstrapAdminOnly = args.Any(argument =>
        argument.Equals("--bootstrap-admin", StringComparison.OrdinalIgnoreCase));
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applyMigrationsOnStartup = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

        if (migrateOnly || applyMigrationsOnStartup)
            db.Database.Migrate();

        if (bootstrapAdminOnly)
        {
            var adminEmail = builder.Configuration["BootstrapAdmin:Email"]?.Trim();
            var adminPassword = builder.Configuration["BootstrapAdmin:Password"];
            var adminFullName = builder.Configuration["BootstrapAdmin:FullName"]?.Trim();

            if (string.IsNullOrWhiteSpace(adminEmail) ||
                string.IsNullOrWhiteSpace(adminPassword) ||
                adminPassword.Length < 12)
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin__Email and BootstrapAdmin__Password with at least 12 characters are required.");
            }

            if (db.Admins.Any())
                throw new InvalidOperationException("Admin bootstrap refused because an admin account already exists.");

            var passwordHashingService = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
            db.Admins.Add(new Admin
            {
                Email = adminEmail,
                PasswordHash = passwordHashingService.HashPassword(adminPassword),
                FullName = string.IsNullOrWhiteSpace(adminFullName) ? "Administrator" : adminFullName,
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            db.SaveChanges();
            Log.Information("Initial admin account created for {AdminEmail}", adminEmail);
        }
    }

    if (migrateOnly || bootstrapAdminOnly)
    {
        if (migrateOnly)
            Log.Information("Database migrations completed successfully");
        return;
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
    app.MapHub<VendingAdSystem.Hubs.DeviceStatusHub>("/hub/device-status");
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
