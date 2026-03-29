namespace SmartBets.Dtos;

public class LeagueDto
{
    public long Id { get; set; }
    public long ApiLeagueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Season { get; set; }
    public long CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
}