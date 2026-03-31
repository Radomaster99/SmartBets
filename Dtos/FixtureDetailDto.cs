namespace SmartBets.Dtos;

public class FixtureDetailDto
{
    public FixtureDto Fixture { get; set; } = new();
    public BestOddsDto? BestOdds { get; set; }
    public DateTime? LatestOddsCollectedAtUtc { get; set; }
    public FixtureFreshnessDto Freshness { get; set; } = new();
    public DateTime? FixturesLiveLastSyncedAtUtc { get; set; }
    public DateTime? FixturesUpcomingLastSyncedAtUtc { get; set; }
    public DateTime? FixturesFullLastSyncedAtUtc { get; set; }
    public DateTime? OddsLastSyncedAtUtc { get; set; }
}
