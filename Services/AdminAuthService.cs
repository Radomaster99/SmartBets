using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class AdminAuthService
{
    public const string AdminCookieScheme = "AdminCookie";
    public const string SessionExpiresAtClaimType = "admin_session_expires_at_utc";

    private readonly IOptionsMonitor<AdminAuthOptions> _optionsMonitor;

    public AdminAuthService(IOptionsMonitor<AdminAuthOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public bool IsConfigured()
    {
        return _optionsMonitor.CurrentValue.HasConfiguredUsers();
    }

    public AdminAuthOptions GetCurrentOptions()
    {
        return _optionsMonitor.CurrentValue;
    }

    public bool TryValidateCredentials(
        string? username,
        string? password,
        out AdminAuthValidatedUser user)
    {
        user = default;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        var configuredUsers = _optionsMonitor.CurrentValue.GetConfiguredUsers();
        foreach (var configuredUser in configuredUsers)
        {
            if (!string.Equals(configuredUser.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            if (!FixedTimeEquals(configuredUser.Password, password))
                return false;

            user = new AdminAuthValidatedUser(
                configuredUser.Username,
                string.IsNullOrWhiteSpace(configuredUser.DisplayName) ? configuredUser.Username : configuredUser.DisplayName.Trim());

            return true;
        }

        return false;
    }

    public ClaimsPrincipal CreatePrincipal(AdminAuthValidatedUser user, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"admin:{user.Username}"),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, "admin"),
            new("auth_source", AdminCookieScheme),
            new("admin_username", user.Username),
            new(SessionExpiresAtClaimType, expiresAtUtc.ToString("O"))
        };

        var identity = new ClaimsIdentity(claims, AdminCookieScheme);
        return new ClaimsPrincipal(identity);
    }

    public static DateTime? GetSessionExpiresAtUtc(ClaimsPrincipal principal)
    {
        var rawValue = principal.FindFirst(SessionExpiresAtClaimType)?.Value;
        return DateTime.TryParse(
            rawValue,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightBytes = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

public readonly record struct AdminAuthValidatedUser(string Username, string DisplayName);
