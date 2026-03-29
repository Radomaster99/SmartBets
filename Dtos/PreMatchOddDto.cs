namespace SmartBets.Dtos;

public class PreMatchOddDto
{
    public long FixtureId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;

    public string Bookmaker { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;

    public decimal? HomeOdd { get; set; }
    public decimal? DrawOdd { get; set; }
    public decimal? AwayOdd { get; set; }

    public DateTime CollectedAt { get; set; }
}