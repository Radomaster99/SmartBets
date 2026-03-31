namespace SmartBets.Dtos;

public class FixtureMatchCenterSyncDto
{
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string StateBucket { get; set; } = string.Empty;
    public bool Forced { get; set; }
    public bool PlayersIncluded { get; set; }
    public bool EventsSynced { get; set; }
    public bool StatisticsSynced { get; set; }
    public bool LineupsSynced { get; set; }
    public bool PlayersSynced { get; set; }
    public IReadOnlyList<string> SkippedComponents { get; set; } = Array.Empty<string>();
    public DateTime ExecutedAtUtc { get; set; }
    public FixtureFreshnessDto Freshness { get; set; } = new();
}

public class LiveMatchCenterSyncDto
{
    public int FixturesConsidered { get; set; }
    public int FixturesSynced { get; set; }
    public bool PlayersIncluded { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public IReadOnlyList<FixtureMatchCenterSyncDto> Items { get; set; } = Array.Empty<FixtureMatchCenterSyncDto>();
}
