using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

namespace ElenskiBalkandzii.Server.API;

public sealed class AdminAccess(IConfiguration configuration)
{
    private string? Password => configuration["Admin:Password"];
    private string? Secret => configuration["Admin:SessionSecret"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Password) &&
        Encoding.UTF8.GetByteCount(Secret ?? "") >= 32;

    public bool CheckPassword(string? candidate)
    {
        if (!IsConfigured || candidate is null) return false;
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(Password!));
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public string IssueToken()
    {
        string payload = $"{DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds()}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        byte[] signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret!), Encoding.UTF8.GetBytes(payload));
        return $"{payload}.{Convert.ToHexString(signature)}";
    }

    public bool ValidToken(string? authorization)
    {
        if (!IsConfigured || authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        string token = authorization[7..];
        string[] parts = token.Split('.');
        if (parts.Length != 3 || !long.TryParse(parts[0], out long expiry) ||
            expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
            expiry > DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds() ||
            parts[1].Length != 32 || parts[2].Length != 64) return false;
        try
        {
            byte[] signature = Convert.FromHexString(parts[2]);
            byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret!), Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            return CryptographicOperations.FixedTimeEquals(signature, expected);
        }
        catch (FormatException) { return false; }
    }
}
