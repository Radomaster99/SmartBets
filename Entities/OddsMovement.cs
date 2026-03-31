namespace SmartBets.Entities;

public class OddsMovement
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long BookmakerId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public int SnapshotCount { get; set; }
    public DateTime? FirstCollectedAtUtc { get; set; }
    public DateTime? LastCollectedAtUtc { get; set; }
    public decimal? HomeDelta { get; set; }
    public decimal? DrawDelta { get; set; }
    public decimal? AwayDelta { get; set; }
    public decimal? HomeChangePercent { get; set; }
    public decimal? DrawChangePercent { get; set; }
    public decimal? AwayChangePercent { get; set; }
    public decimal? HomeSwing { get; set; }
    public decimal? DrawSwing { get; set; }
    public decimal? AwaySwing { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}
