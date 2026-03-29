namespace SmartBets.Dtos;

public class LeagueCoverageDto
{
    public long ApiLeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public string CountryName { get; set; } = string.Empty;

    public int FixturesCount { get; set; }
    public int UpcomingCount { get; set; }
    public int FinishedCount { get; set; }
    public int LiveCount { get; set; }
}