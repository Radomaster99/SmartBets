namespace SmartBets.Entities;

public class PreMatchOdd
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long BookmakerId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public decimal? HomeOdd { get; set; }
    public decimal? DrawOdd { get; set; }
    public decimal? AwayOdd { get; set; }
    public DateTime CollectedAt { get; set; }

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}