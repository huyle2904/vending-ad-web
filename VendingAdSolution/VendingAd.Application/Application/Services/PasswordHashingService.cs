using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace VendingAdSystem.Application.Services;

public interface IPasswordHashingService
{
    string HashPassword(string password);
    PasswordVerificationResult VerifyPassword(string passwordHash, string providedPassword);
}

public sealed class PasswordHashingService : IPasswordHashingService
{
    private static readonly PasswordHasher<object> PasswordHasher = new();
    private static readonly object PasswordHasherUser = new();

    public string HashPassword(string password)
    {
        return PasswordHasher.HashPassword(PasswordHasherUser, password);
    }

    public PasswordVerificationResult VerifyPassword(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationResult.Failed;

        try
        {
            var result = PasswordHasher.VerifyHashedPassword(PasswordHasherUser, passwordHash, providedPassword);
            if (result != PasswordVerificationResult.Failed)
                return result;
        }
        catch (FormatException)
        {
            // Legacy SHA256 hashes are also base64, but malformed data should simply fail verification.
        }

        return VerifyLegacySha256Password(passwordHash, providedPassword)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }

    private static bool VerifyLegacySha256Password(string passwordHash, string providedPassword)
    {
        Span<byte> storedHash = stackalloc byte[32];
        if (!Convert.TryFromBase64String(passwordHash, storedHash, out var bytesWritten) || bytesWritten != 32)
            return false;

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedPassword));
        return CryptographicOperations.FixedTimeEquals(storedHash[..bytesWritten], providedHash);
    }
}
