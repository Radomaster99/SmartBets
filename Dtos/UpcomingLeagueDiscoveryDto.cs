namespace SmartBets.Dtos;

public class UpcomingLeagueDiscoveryDto
{
    public long ApiLeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public int UpcomingFixturesCount { get; set; }
}