namespace SmartBets.Dtos;

public class TeamDto
{
    public long Id { get; set; }
    public long ApiTeamId { get; set; }
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

    public long? CountryId { get; set; }
    public string? CountryName { get; set; }
}
