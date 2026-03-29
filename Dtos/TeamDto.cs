namespace SmartBets.Dtos;

public class TeamDto
{
    public long Id { get; set; }
    public long ApiTeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }

    public long? CountryId { get; set; }
    public string? CountryName { get; set; }
}