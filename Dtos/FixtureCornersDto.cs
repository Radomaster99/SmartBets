namespace SmartBets.Dtos;

public class FixtureCornersTeamDto
{
    public long? TeamId { get; set; }
    public long TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public int? Corners { get; set; }
}

public class FixtureCornersDto
{
    public long ApiFixtureId { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public bool HasData { get; set; }
    public int? TotalCorners { get; set; }
    public FixtureCornersTeamDto Home { get; set; } = new();
    public FixtureCornersTeamDto Away { get; set; } = new();
}

public class FixtureCornersSyncDto
{
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string StateBucket { get; set; } = string.Empty;
    public bool Forced { get; set; }
    public bool StatisticsSynced { get; set; }
    public IReadOnlyList<string> SkippedComponents { get; set; } = Array.Empty<string>();
    public DateTime ExecutedAtUtc { get; set; }
    public FixtureFreshnessDto Freshness { get; set; } = new();
    public FixtureCornersDto Corners { get; set; } = new();
}
