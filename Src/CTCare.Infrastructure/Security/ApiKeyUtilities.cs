using System.Security.Cryptography;
using System.Text;

namespace CTCare.Infrastructure.Security;

public static class ApiKeyUtilities
{
    public static string GetPrefix(string apiKey, int length = 8)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        return apiKey.Length <= length ? apiKey : apiKey[..length];
    }

    public static string Hash(string apiKey)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash); // UPPERCASE hex
    }

    // Constant-time comparison to avoid timing attacks
    public static bool Verify(string apiKey, string storedHexHash)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(storedHexHash))
        {
            return false;
        }

        // Normalize stored hash
        var normalized = storedHexHash.Replace("-", "").Trim();
        var provided = Hash(apiKey);

        // Fixed-time check (compare bytes)
        var a = Encoding.ASCII.GetBytes(provided);
        var b = Encoding.ASCII.GetBytes(normalized);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
