namespace SmartBets.Entities;

public class TheOddsLiveOdd
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public string SportKey { get; set; } = string.Empty;
    public string BookmakerKey { get; set; } = string.Empty;
    public string BookmakerTitle { get; set; } = string.Empty;
    public string MarketKey { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;
    public string OutcomeName { get; set; } = string.Empty;
    public decimal? Point { get; set; }
    public decimal? Price { get; set; }
    public DateTime? LastUpdateUtc { get; set; }
    public DateTime CollectedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
