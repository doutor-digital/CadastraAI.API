using System.Security.Cryptography;

namespace CadastraAI.API.Auth;

public static class InviteTokenGenerator
{
    /// <summary>
    /// 32 bytes of cryptographic randomness, URL-safe Base64 — yields 43 chars.
    /// Fits in the Token's MaxLength(64).
    /// </summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
