namespace SmartBets.Entities;

public class League
{
    public long Id { get; set; }
    public long ApiLeagueId { get; set; }
    public long CountryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Season { get; set; }

    public Country Country { get; set; } = null!;
    public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
    public ICollection<Standing> Standings { get; set; } = new List<Standing>();
}