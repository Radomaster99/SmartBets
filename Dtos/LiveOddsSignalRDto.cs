namespace SmartBets.Dtos;

public class LiveOddsUpdatedDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public DateTime CollectedAtUtc { get; set; }
    public IReadOnlyList<LiveOddsMarketDto> Markets { get; set; } = Array.Empty<LiveOddsMarketDto>();
}
