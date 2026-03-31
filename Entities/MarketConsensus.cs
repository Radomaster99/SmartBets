namespace SmartBets.Entities;

public class MarketConsensus
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public int SampleSize { get; set; }
    public decimal? OpeningHomeConsensusOdd { get; set; }
    public decimal? OpeningDrawConsensusOdd { get; set; }
    public decimal? OpeningAwayConsensusOdd { get; set; }
    public decimal? LatestHomeConsensusOdd { get; set; }
    public decimal? LatestDrawConsensusOdd { get; set; }
    public decimal? LatestAwayConsensusOdd { get; set; }
    public decimal? BestHomeOdd { get; set; }
    public decimal? BestDrawOdd { get; set; }
    public decimal? BestAwayOdd { get; set; }
    public long? BestHomeBookmakerId { get; set; }
    public long? BestDrawBookmakerId { get; set; }
    public long? BestAwayBookmakerId { get; set; }
    public decimal? MaxHomeSpread { get; set; }
    public decimal? MaxDrawSpread { get; set; }
    public decimal? MaxAwaySpread { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
