namespace SmartBets.Dtos;

public class OddDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public long BookmakerId { get; set; }
    public long ApiBookmakerId { get; set; }
    public string Bookmaker { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;
    public decimal? HomeOdd { get; set; }
    public decimal? DrawOdd { get; set; }
    public decimal? AwayOdd { get; set; }
    public DateTime CollectedAtUtc { get; set; }
}
