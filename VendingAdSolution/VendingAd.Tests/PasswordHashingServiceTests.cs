using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using Xunit;

namespace VendingAd.Tests;

public class PasswordHashingServiceTests
{
    [Fact]
    public void HashPassword_UsesIdentityHasherAndVerifiesPassword()
    {
        var service = new PasswordHashingService();

        var hash = service.HashPassword("secret-password");

        Assert.NotEqual(CreateLegacySha256Hash("secret-password"), hash);
        Assert.Equal(PasswordVerificationResult.Success, service.VerifyPassword(hash, "secret-password"));
        Assert.Equal(PasswordVerificationResult.Failed, service.VerifyPassword(hash, "wrong-password"));
    }

    [Fact]
    public void VerifyPassword_WhenHashIsLegacySha256_ReturnsRehashNeeded()
    {
        var service = new PasswordHashingService();
        var legacyHash = CreateLegacySha256Hash("legacy-password");

        var result = service.VerifyPassword(legacyHash, "legacy-password");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public async Task LoginUserAsync_WhenHashIsLegacySha256_RehashesPassword()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var passwordHashingService = new PasswordHashingService();
        var legacyHash = CreateLegacySha256Hash("legacy-password");
        context.Users.Add(new User
        {
            Username = "legacy-user",
            Email = "legacy@example.com",
            PasswordHash = legacyHash,
            FullName = "Legacy User",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var authService = new AuthService(
            new Repository<User>(context),
            new Repository<Admin>(context),
            passwordHashingService);

        var response = await authService.LoginUserAsync(new LoginRequest
        {
            Username = "legacy-user",
            Password = "legacy-password"
        });

        var user = await context.Users.SingleAsync();
        Assert.True(response.Success);
        Assert.NotEqual(legacyHash, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, passwordHashingService.VerifyPassword(user.PasswordHash, "legacy-password"));
    }

    private static string CreateLegacySha256Hash(string password)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }
}
