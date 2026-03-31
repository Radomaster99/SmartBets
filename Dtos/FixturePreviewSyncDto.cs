namespace SmartBets.Dtos;

public class FixturePreviewSyncDto
{
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string Stage { get; set; } = string.Empty;
    public bool Forced { get; set; }
    public bool PredictionSynced { get; set; }
    public bool InjuriesSynced { get; set; }
    public IReadOnlyList<string> SkippedComponents { get; set; } = Array.Empty<string>();
    public DateTime ExecutedAtUtc { get; set; }
    public FixtureFreshnessDto Freshness { get; set; } = new();
}

public class UpcomingPreviewSyncDto
{
    public int FixturesConsidered { get; set; }
    public int FixturesSynced { get; set; }
    public int WindowHours { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public IReadOnlyList<FixturePreviewSyncDto> Items { get; set; } = Array.Empty<FixturePreviewSyncDto>();
}
