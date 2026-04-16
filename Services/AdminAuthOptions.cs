using Microsoft.AspNetCore.Http;

namespace SmartBets.Services;

public class AdminAuthOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string CookieName { get; set; } = "smartbets_admin";
    public int SessionHours { get; set; } = 12;
    public string CookieSameSite { get; set; } = nameof(SameSiteMode.Lax);
    public string? CookieDomain { get; set; }
    public List<AdminAuthUserOptions> Users { get; set; } = new();

    public IReadOnlyList<AdminAuthUserOptions> GetConfiguredUsers()
    {
        var configuredUsers = new List<AdminAuthUserOptions>();

        if (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password))
        {
            configuredUsers.Add(new AdminAuthUserOptions
            {
                Username = Username.Trim(),
                Password = Password,
                DisplayName = Username.Trim()
            });
        }

        configuredUsers.AddRange(
            Users
                .Where(x => !string.IsNullOrWhiteSpace(x.Username) && !string.IsNullOrWhiteSpace(x.Password))
                .Select(x => new AdminAuthUserOptions
                {
                    Username = x.Username.Trim(),
                    Password = x.Password,
                    DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.Username.Trim() : x.DisplayName.Trim()
                }));

        return configuredUsers
            .GroupBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    public bool HasConfiguredUsers()
    {
        return GetConfiguredUsers().Count > 0;
    }

    public string GetCookieName()
    {
        return string.IsNullOrWhiteSpace(CookieName)
            ? "smartbets_admin"
            : CookieName.Trim();
    }

    public TimeSpan GetSessionLifetime()
    {
        return TimeSpan.FromHours(Math.Clamp(SessionHours, 1, 168));
    }

    public SameSiteMode GetCookieSameSite()
    {
        return Enum.TryParse<SameSiteMode>(CookieSameSite, ignoreCase: true, out var sameSiteMode)
            ? sameSiteMode
            : SameSiteMode.Lax;
    }
}

public class AdminAuthUserOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
