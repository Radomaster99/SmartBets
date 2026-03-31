namespace SmartBets.Dtos;

public class OddsHistoryPointDto
{
    public DateTime CollectedAtUtc { get; set; }
    public decimal? HomeOdd { get; set; }
    public decimal? DrawOdd { get; set; }
    public decimal? AwayOdd { get; set; }
}

public class OddsHistorySeriesDto
{
    public long BookmakerId { get; set; }
    public long ApiBookmakerId { get; set; }
    public string Bookmaker { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;
    public int SnapshotCount { get; set; }
    public decimal? OpeningHomeOdd { get; set; }
    public decimal? OpeningDrawOdd { get; set; }
    public decimal? OpeningAwayOdd { get; set; }
    public decimal? LatestHomeOdd { get; set; }
    public decimal? LatestDrawOdd { get; set; }
    public decimal? LatestAwayOdd { get; set; }
    public decimal? PeakHomeOdd { get; set; }
    public decimal? PeakDrawOdd { get; set; }
    public decimal? PeakAwayOdd { get; set; }
    public decimal? ClosingHomeOdd { get; set; }
    public decimal? ClosingDrawOdd { get; set; }
    public decimal? ClosingAwayOdd { get; set; }
    public IReadOnlyList<OddsHistoryPointDto> Points { get; set; } = Array.Empty<OddsHistoryPointDto>();
}

public class FixtureOddsHistoryDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public DateTime? LatestCollectedAtUtc { get; set; }
    public IReadOnlyList<OddsHistorySeriesDto> Series { get; set; } = Array.Empty<OddsHistorySeriesDto>();
}

public class OddsMovementDto
{
    public long BookmakerId { get; set; }
    public long ApiBookmakerId { get; set; }
    public string Bookmaker { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;
    public int SnapshotCount { get; set; }
    public DateTime? FirstCollectedAtUtc { get; set; }
    public DateTime? LastCollectedAtUtc { get; set; }
    public DateTime? ClosingCollectedAtUtc { get; set; }
    public decimal? OpeningHomeOdd { get; set; }
    public decimal? OpeningDrawOdd { get; set; }
    public decimal? OpeningAwayOdd { get; set; }
    public decimal? LatestHomeOdd { get; set; }
    public decimal? LatestDrawOdd { get; set; }
    public decimal? LatestAwayOdd { get; set; }
    public decimal? PeakHomeOdd { get; set; }
    public decimal? PeakDrawOdd { get; set; }
    public decimal? PeakAwayOdd { get; set; }
    public decimal? ClosingHomeOdd { get; set; }
    public decimal? ClosingDrawOdd { get; set; }
    public decimal? ClosingAwayOdd { get; set; }
    public decimal? HomeDelta { get; set; }
    public decimal? DrawDelta { get; set; }
    public decimal? AwayDelta { get; set; }
    public decimal? HomeChangePercent { get; set; }
    public decimal? DrawChangePercent { get; set; }
    public decimal? AwayChangePercent { get; set; }
    public decimal? HomeSwing { get; set; }
    public decimal? DrawSwing { get; set; }
    public decimal? AwaySwing { get; set; }
}

public class OddsConsensusDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public int SampleSize { get; set; }
    public decimal? OpeningHomeConsensusOdd { get; set; }
    public decimal? OpeningDrawConsensusOdd { get; set; }
    public decimal? OpeningAwayConsensusOdd { get; set; }
    public decimal? LatestHomeConsensusOdd { get; set; }
    public decimal? LatestDrawConsensusOdd { get; set; }
    public decimal? LatestAwayConsensusOdd { get; set; }
    public decimal? BestHomeOdd { get; set; }
    public string? BestHomeBookmaker { get; set; }
    public decimal? BestDrawOdd { get; set; }
    public string? BestDrawBookmaker { get; set; }
    public decimal? BestAwayOdd { get; set; }
    public string? BestAwayBookmaker { get; set; }
    public decimal? MaxHomeSpread { get; set; }
    public decimal? MaxDrawSpread { get; set; }
    public decimal? MaxAwaySpread { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class OddsValueSignalItemDto
{
    public string Outcome { get; set; } = string.Empty;
    public decimal? OpeningConsensusOdd { get; set; }
    public decimal? LatestConsensusOdd { get; set; }
    public decimal? BestOdd { get; set; }
    public string? BestBookmaker { get; set; }
    public decimal? MarketDelta { get; set; }
    public decimal? MarketDeltaPercent { get; set; }
    public decimal? ValueEdge { get; set; }
    public decimal? ValueEdgePercent { get; set; }
    public decimal? MaxSpread { get; set; }
    public bool HasPositiveEdge { get; set; }
}

public class OddsValueSignalsDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<OddsValueSignalItemDto> Signals { get; set; } = Array.Empty<OddsValueSignalItemDto>();
}

public class OddsAnalyticsRebuildResultDto
{
    public string MarketName { get; set; } = string.Empty;
    public int FixturesProcessed { get; set; }
    public int OpenCloseRowsUpserted { get; set; }
    public int MovementRowsUpserted { get; set; }
    public int ConsensusRowsUpserted { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}
