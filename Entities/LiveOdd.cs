namespace SmartBets.Entities;

public class LiveOdd
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long BookmakerId { get; set; }
    public long ApiBetId { get; set; }
    public string BetName { get; set; } = string.Empty;
    public string OutcomeLabel { get; set; } = string.Empty;
    public string? Line { get; set; }
    public decimal? Odd { get; set; }
    public bool? IsMain { get; set; }
    public bool? Stopped { get; set; }
    public bool? Blocked { get; set; }
    public bool? Finished { get; set; }
    public DateTime CollectedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
    public LiveBetType? LiveBetType { get; set; }
}
