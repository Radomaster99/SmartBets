namespace SmartBets.Services;

public class TheOddsApiOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "https://api.the-odds-api.com/v4";
    public string ApiKey { get; set; } = string.Empty;
    public string Regions { get; set; } = "uk,eu";
    public string MarketKey { get; set; } = "h2h";
    public string OddsFormat { get; set; } = "decimal";
    public string DateFormat { get; set; } = "iso";
    public int FreshnessSeconds { get; set; } = 45;
    public int MatchToleranceMinutes { get; set; } = 360;
    public bool EnableViewerDrivenRefresh { get; set; } = true;
    public bool EnableReadDrivenCatchUp { get; set; } = false;
    public int ViewerHeartbeatTtlSeconds { get; set; } = 60;
    public int ViewerRefreshIntervalSeconds { get; set; } = 180;
    public int MaxViewerFixturesPerCycle { get; set; } = 8;
    public int PriorityKeepaliveCount { get; set; } = 0;
    public int MinLeagueSyncIntervalSeconds { get; set; } = 600;
    public Dictionary<long, string> LeagueSportKeys { get; set; } = new();

    public bool IsConfigured() =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public string GetBaseUrl() =>
        string.IsNullOrWhiteSpace(BaseUrl)
            ? "https://api.the-odds-api.com/v4"
            : BaseUrl.TrimEnd('/');

    public string GetRegions() =>
        string.IsNullOrWhiteSpace(Regions)
            ? "uk,eu"
            : string.Join(',',
                Regions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase));

    public string GetMarketKey() =>
        string.IsNullOrWhiteSpace(MarketKey)
            ? "h2h"
            : MarketKey.Trim().ToLowerInvariant();

    public string GetOddsFormat() =>
        string.IsNullOrWhiteSpace(OddsFormat)
            ? "decimal"
            : OddsFormat.Trim().ToLowerInvariant();

    public string GetDateFormat() =>
        string.IsNullOrWhiteSpace(DateFormat)
            ? "iso"
            : DateFormat.Trim().ToLowerInvariant();

    public TimeSpan GetFreshnessInterval() =>
        TimeSpan.FromSeconds(Math.Clamp(FreshnessSeconds, 15, 300));

    public TimeSpan GetMatchTolerance() =>
        TimeSpan.FromMinutes(Math.Clamp(MatchToleranceMinutes, 15, 720));

    public TimeSpan GetViewerHeartbeatTtl() =>
        TimeSpan.FromSeconds(Math.Clamp(ViewerHeartbeatTtlSeconds, 30, 300));

    public TimeSpan GetViewerRefreshInterval() =>
        TimeSpan.FromSeconds(Math.Clamp(ViewerRefreshIntervalSeconds, 30, 300));

    public int GetMaxViewerFixturesPerCycle() =>
        Math.Clamp(MaxViewerFixturesPerCycle, 1, 100);

    public int GetPriorityKeepaliveCount() =>
        Math.Clamp(PriorityKeepaliveCount, 0, 20);

    public TimeSpan GetMinLeagueSyncInterval() =>
        TimeSpan.FromSeconds(Math.Clamp(MinLeagueSyncIntervalSeconds, 30, 1800));

    public bool ShouldAllowReadDrivenCatchUp() =>
        EnableReadDrivenCatchUp || !EnableViewerDrivenRefresh;

    public bool TryGetSportKey(long leagueApiId, out string sportKey)
    {
        if (LeagueSportKeys.TryGetValue(leagueApiId, out var configuredSportKey) &&
            !string.IsNullOrWhiteSpace(configuredSportKey))
        {
            sportKey = configuredSportKey.Trim();
            return true;
        }

        sportKey = string.Empty;
        return false;
    }
}
