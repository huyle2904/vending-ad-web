using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using VendingAdSystem.Middleware;
using Xunit;

namespace VendingAd.Tests;

public class SecurityIntegrationTests
{
    [Fact]
    public async Task RequestWithoutCorrelationId_ReturnsGeneratedHeader()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/account/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeader, out var values));
        Assert.Matches("^[a-f0-9]{32}$", Assert.Single(values));
    }

    [Fact]
    public async Task RequestWithCorrelationId_PreservesIncomingHeader()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/account/login");
        request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeader, "test-correlation-id");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeader, out var values));
        Assert.Equal("test-correlation-id", Assert.Single(values));
    }

    [Fact]
    public async Task AnonymousAdminRequest_RedirectsToLogin()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task UserRole_CannotViewAdminDashboard()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.UseTestUser(role: "User", userId: 10);

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MobileDeviceRequest_MissingDeviceSecret_ReturnsUnauthorized()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedDeviceAsync("DEVICE-001", "correct-secret");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/mobile/devices/DEVICE-001");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MobileDeviceRequest_WrongDeviceSecret_ReturnsUnauthorized()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedDeviceAsync("DEVICE-001", "correct-secret");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Secret", "wrong-secret");

        var response = await client.GetAsync("/api/mobile/devices/DEVICE-001");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MobileDeviceInfo_WhenRequestLimitExceeded_ReturnsTooManyRequests()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedDeviceAsync("DEVICE-001", "correct-secret");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Secret", "correct-secret");

        var first = await client.GetAsync("/api/mobile/devices/DEVICE-001");
        var second = await client.GetAsync("/api/mobile/devices/DEVICE-001");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.RetryAfter?.Delta?.TotalSeconds >= 1);
    }

    [Fact]
    public async Task PortalPlaylist_WhenRequestLimitExceeded_ReturnsTooManyRequests()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedDeviceAsync("DEVICE-001", "correct-secret");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Secret", "correct-secret");

        var first = await client.GetAsync("/api/portal/playlist/DEVICE-001");
        var second = await client.GetAsync("/api/portal/playlist/DEVICE-001");

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.RetryAfter?.Delta?.TotalSeconds >= 1);
    }

    [Fact]
    public async Task PortalUpload_IgnoresForgedUserIdFormField()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: true);
        await factory.SeedUserAsync(1);
        await factory.SeedUserAsync(999);
        var client = factory.CreateClient();
        client.UseTestUser(role: "User", userId: 1);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("999"), "userId");
        var file = new ByteArrayContent(CreateMinimalMp4Header());
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        form.Add(file, "file", "clip.mp4");

        var response = await client.PostAsync("/api/portal/upload", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var media = await db.Medias.OrderByDescending(m => m.Id).FirstAsync();
        var auditLog = await db.AuditLogs.OrderByDescending(log => log.Id).FirstAsync();
        Assert.Equal(1, media.UserId);
        Assert.StartsWith("/uploads/", media.FileUrl);
        Assert.Equal(AuditActions.UploadVideo, auditLog.Action);
        Assert.Equal(AuditTargets.Media, auditLog.TargetType);
        Assert.Equal(media.Id, auditLog.TargetId);
        Assert.Equal(AuditActorTypes.User, auditLog.ActorType);
        Assert.Equal(1, auditLog.ActorId);
    }

    [Fact]
    public async Task AuthApiLoginUser_WhenCredentialsAreValid_WritesAuditLog()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedUserAsync(7, password: "Secret123!");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login/user", new LoginRequest
        {
            Username = "user-7",
            Password = "Secret123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditLog = await db.AuditLogs.OrderByDescending(log => log.Id).FirstAsync();
        Assert.Equal(AuditActions.Login, auditLog.Action);
        Assert.Equal(AuditTargets.User, auditLog.TargetType);
        Assert.Equal(7, auditLog.TargetId);
        Assert.Equal(AuditActorTypes.User, auditLog.ActorType);
        Assert.Equal(7, auditLog.ActorId);
    }

    [Fact]
    public async Task AuthApiLoginUser_WhenCredentialsAreInvalid_WritesFailedAuditLog()
    {
        await using var factory = new VendingAdWebApplicationFactory(useTestAuth: false);
        await factory.SeedUserAsync(8, password: "Secret123!");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login/user", new LoginRequest
        {
            Username = "user-8",
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditLog = await db.AuditLogs.OrderByDescending(log => log.Id).FirstAsync();
        Assert.Equal(AuditActions.LoginFailed, auditLog.Action);
        Assert.Equal(AuditTargets.Account, auditLog.TargetType);
        Assert.Equal(AuditActorTypes.Anonymous, auditLog.ActorType);
        Assert.Null(auditLog.ActorId);
    }

    [Fact]
    public async Task PortalUpload_WhenFfprobeEnabled_StoresVideoDuration()
    {
        var ffprobePath = CreateFakeFfprobe();
        try
        {
            await using var factory = new VendingAdWebApplicationFactory(
                useTestAuth: true,
                ffprobeEnabled: true,
                requireFfprobe: true,
                ffprobePath: ffprobePath);
            await factory.SeedUserAsync(1);
            var client = factory.CreateClient();
            client.UseTestUser(role: "User", userId: 1);

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(CreateMinimalMp4Header());
            file.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
            form.Add(file, "file", "clip.mp4");

            var response = await client.PostAsync("/api/portal/upload", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var media = await db.Medias.OrderByDescending(m => m.Id).FirstAsync();
            Assert.Equal(13, media.DurationSeconds);
        }
        finally
        {
            if (File.Exists(ffprobePath))
                File.Delete(ffprobePath);
        }
    }

    private static byte[] CreateMinimalMp4Header()
    {
        return new byte[]
        {
            0x00, 0x00, 0x00, 0x18,
            (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'i', (byte)'s', (byte)'o', (byte)'m',
            0x00, 0x00, 0x02, 0x00
        };
    }

    private sealed class VendingAdWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly bool _useTestAuth;
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"vendingad-tests-{Guid.NewGuid():N}.db");
        private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), $"vendingad-uploads-{Guid.NewGuid():N}");

        private readonly bool _ffprobeEnabled;
        private readonly bool _requireFfprobe;
        private readonly string? _ffprobePath;

        public VendingAdWebApplicationFactory(
            bool useTestAuth,
            bool ffprobeEnabled = false,
            bool requireFfprobe = false,
            string? ffprobePath = null)
        {
            _useTestAuth = useTestAuth;
            _ffprobeEnabled = ffprobeEnabled;
            _requireFfprobe = requireFfprobe;
            _ffprobePath = ffprobePath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Database:EnsureCreatedOnStartup"] = "true",
                    ["Seed:EnableDemoData"] = "false",
                    ["Redis:Enabled"] = "false",
                    ["RabbitMQ:Enabled"] = "false",
                    ["UploadsPath"] = _uploadsPath,
                    ["MobileRateLimiting:WindowSeconds"] = "60",
                    ["MobileRateLimiting:DeviceInfoPermitLimit"] = "1",
                    ["MobileRateLimiting:HeartbeatPermitLimit"] = "1",
                    ["MobileRateLimiting:PlaybackStatePermitLimit"] = "1",
                    ["MobileRateLimiting:PlaylistPermitLimit"] = "1",
                    ["VideoValidation:FfprobeEnabled"] = _ffprobeEnabled.ToString(),
                    ["VideoValidation:RequireFfprobe"] = _requireFfprobe.ToString(),
                    ["VideoValidation:FfprobePath"] = _ffprobePath ?? "ffprobe",
                    ["VideoValidation:ProbeTimeoutSeconds"] = "5",
                    ["VideoValidation:AllowedVideoCodecs:0"] = "h264",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
                    ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={_databasePath}");
                });

                if (_useTestAuth)
                {
                    services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName,
                            _ => { });
                }
            });
        }

        public async Task SeedUserAsync(int userId, string? password = null)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.Users.AnyAsync(u => u.Id == userId))
                return;

            var passwordHash = password == null
                ? "unused"
                : new PasswordHashingService().HashPassword(password);

            db.Users.Add(new User
            {
                Id = userId,
                Username = $"user-{userId}",
                Email = $"user-{userId}@example.test",
                FullName = $"User {userId}",
                PasswordHash = passwordHash,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedDeviceAsync(string deviceCode, string secret)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var credentialService = new DeviceCredentialService(
                new Repository<Device>(db),
                new PasswordHashingService(),
                NullAuditService.Instance);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceCode == deviceCode);
            if (device == null)
            {
                device = new Device
                {
                    DeviceCode = deviceCode,
                    IsActive = true
                };
                db.Devices.Add(device);
            }

            credentialService.AssignSecret(device, secret, DateTime.UtcNow);

            await db.SaveChangesAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();

            if (File.Exists(_databasePath))
                File.Delete(_databasePath);

            if (Directory.Exists(_uploadsPath))
                Directory.Delete(_uploadsPath, recursive: true);
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValues))
                return Task.FromResult(AuthenticateResult.NoResult());

            var role = roleValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var id = Request.Headers.TryGetValue("X-Test-User-Id", out var idValues)
                ? idValues.FirstOrDefault() ?? "1"
                : "1";

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, id),
                new(ClaimTypes.Name, $"test-{role.ToLowerInvariant()}-{id}"),
                new(ClaimTypes.Email, $"test-{role.ToLowerInvariant()}-{id}@example.test"),
                new(ClaimTypes.Role, role)
            };

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                claims.Add(new Claim("AdminId", id));

            if (role.Equals("User", StringComparison.OrdinalIgnoreCase))
                claims.Add(new Claim("UserId", id));

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private static string CreateFakeFfprobe()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"fake-ffprobe-{Guid.NewGuid():N}.sh");
        File.WriteAllText(scriptPath, """
            #!/usr/bin/env sh
            cat <<'JSON'
            {"streams":[{"codec_type":"video","codec_name":"h264","duration":"12.4"}],"format":{"duration":"12.4"}}
            JSON
            """);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }

        return scriptPath;
    }
}

internal static class TestHttpClientExtensions
{
    public static void UseTestUser(this HttpClient client, string role, int userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
    }
}
