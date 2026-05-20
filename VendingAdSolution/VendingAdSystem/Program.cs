using Microsoft.AspNetCore.Authentication.Cookies;
using VendingAdSystem.Infrastructure;
using VendingAdSystem.Infrastructure.Health;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Startup database initialization strategy.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var applyMigrationsOnStartup = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
    var ensureCreatedOnStartup = builder.Configuration.GetValue<bool>("Database:EnsureCreatedOnStartup");
    var resetOnStartup = builder.Configuration.GetValue<bool>("Database:ResetOnStartup");
    var seedDemoData = builder.Configuration.GetValue<bool>("Seed:EnableDemoData");

    if (seedDemoData && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Seed:EnableDemoData must be false outside Development.");

    if (resetOnStartup)
    {
        if (!app.Environment.IsDevelopment())
            app.Logger.LogWarning("Database:ResetOnStartup is enabled outside Development. The configured database will be deleted and recreated.");

        if (!applyMigrationsOnStartup && !ensureCreatedOnStartup)
            throw new InvalidOperationException("Database:ResetOnStartup requires Database:ApplyMigrationsOnStartup=true or Database:EnsureCreatedOnStartup=true.");

        db.Database.EnsureDeleted();
    }

    if (applyMigrationsOnStartup)
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
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

public partial class Program { }
