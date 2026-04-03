using System.Security.Cryptography;
using System.Text;

namespace SmartBets.Services;

public static class JwtSigningKeyHelper
{
    private const string DevelopmentPlaceholder = "development-only-placeholder-signing-key-for-smartbets";

    public static byte[] ResolveSigningKeyBytes(string? jwtSigningKey, string? apiKeyToken)
    {
        var source = !string.IsNullOrWhiteSpace(jwtSigningKey)
            ? jwtSigningKey.Trim()
            : apiKeyToken?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            source = DevelopmentPlaceholder;
        }

        var rawBytes = Encoding.UTF8.GetBytes(source);
        if (rawBytes.Length >= 32)
        {
            return rawBytes;
        }

        return SHA256.HashData(rawBytes);
    }
}
