namespace SmartBets.Dtos;

public class LeagueSeasonCoverageDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public LeagueCoverageFlagsDto Coverage { get; set; } = new();
}
