namespace SmartBets.Entities;

public class Team
{
    public long Id { get; set; }
    public long ApiTeamId { get; set; }
    public long? CountryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
    public int? Founded { get; set; }
    public bool? IsNational { get; set; }
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }
    public string? VenueCity { get; set; }
    public int? VenueCapacity { get; set; }
    public string? VenueSurface { get; set; }
    public string? VenueImageUrl { get; set; }

    public Country? Country { get; set; }
    public ICollection<Standing> Standings { get; set; } = new List<Standing>();
    public ICollection<TeamStatistic> Statistics { get; set; } = new List<TeamStatistic>();
}
