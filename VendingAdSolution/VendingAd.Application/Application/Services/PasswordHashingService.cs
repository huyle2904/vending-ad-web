using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace VendingAdSystem.Application.Services;

public interface IPasswordHashingService
{
    string HashPassword(string password);
    PasswordVerificationResult VerifyPassword(string passwordHash, string providedPassword);
}

public interface ITemporaryPasswordGenerator
{
    string Generate();
}

public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@$%*-_";
    private const string AllCharacters = Uppercase + Lowercase + Digits + Symbols;

    public string Generate()
    {
        Span<char> characters = stackalloc char[16];
        characters[0] = RandomCharacter(Uppercase);
        characters[1] = RandomCharacter(Lowercase);
        characters[2] = RandomCharacter(Digits);
        characters[3] = RandomCharacter(Symbols);

        for (var index = 4; index < characters.Length; index++)
            characters[index] = RandomCharacter(AllCharacters);

        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters);
    }

    private static char RandomCharacter(string alphabet)
    {
        return alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
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
