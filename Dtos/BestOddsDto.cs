namespace SmartBets.Dtos;

public class BestOddsDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public DateTime? CollectedAtUtc { get; set; }

    public decimal? BestHomeOdd { get; set; }
    public string? BestHomeBookmaker { get; set; }

    public decimal? BestDrawOdd { get; set; }
    public string? BestDrawBookmaker { get; set; }

    public decimal? BestAwayOdd { get; set; }
    public string? BestAwayBookmaker { get; set; }
}
