namespace SmartBets.Dtos;

public class TeamFormItemDto
{
    public long ApiFixtureId { get; set; }
    public DateTime KickoffAtUtc { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public string? OpponentLogoUrl { get; set; }
    public bool IsHome { get; set; }
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public string Result { get; set; } = string.Empty;
}

public class TeamRecentFormDto
{
    public long TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public string Form { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public IReadOnlyList<TeamFormItemDto> Fixtures { get; set; } = Array.Empty<TeamFormItemDto>();
}
