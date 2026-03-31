namespace SmartBets.Entities;

public class OddsOpenClose
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long BookmakerId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public int SnapshotCount { get; set; }
    public decimal? OpeningHomeOdd { get; set; }
    public decimal? OpeningDrawOdd { get; set; }
    public decimal? OpeningAwayOdd { get; set; }
    public DateTime? OpeningCollectedAtUtc { get; set; }
    public decimal? LatestHomeOdd { get; set; }
    public decimal? LatestDrawOdd { get; set; }
    public decimal? LatestAwayOdd { get; set; }
    public DateTime? LatestCollectedAtUtc { get; set; }
    public decimal? PeakHomeOdd { get; set; }
    public decimal? PeakDrawOdd { get; set; }
    public decimal? PeakAwayOdd { get; set; }
    public DateTime? PeakHomeCollectedAtUtc { get; set; }
    public DateTime? PeakDrawCollectedAtUtc { get; set; }
    public DateTime? PeakAwayCollectedAtUtc { get; set; }
    public decimal? ClosingHomeOdd { get; set; }
    public decimal? ClosingDrawOdd { get; set; }
    public decimal? ClosingAwayOdd { get; set; }
    public DateTime? ClosingCollectedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}
