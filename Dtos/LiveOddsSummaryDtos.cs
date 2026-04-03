namespace SmartBets.Dtos;

public class LiveOddsSummaryRequestDto
{
    public IReadOnlyList<long> FixtureIds { get; set; } = Array.Empty<long>();
}

public class FixtureLiveOddsSummaryDto
{
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public string Source { get; set; } = "none";
    public DateTime? CollectedAtUtc { get; set; }
    public decimal? BestHomeOdd { get; set; }
    public string? BestHomeBookmaker { get; set; }
    public decimal? BestDrawOdd { get; set; }
    public string? BestDrawBookmaker { get; set; }
    public decimal? BestAwayOdd { get; set; }
    public string? BestAwayBookmaker { get; set; }
}
