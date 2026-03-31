namespace SmartBets.Dtos;

public class HeadToHeadItemDto
{
    public long ApiFixtureId { get; set; }
    public DateTime KickoffAtUtc { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string HomeTeamName { get; set; } = string.Empty;
    public string? HomeTeamLogoUrl { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public string? AwayTeamLogoUrl { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
}

public class FixtureHeadToHeadDto
{
    public long HomeTeamApiId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public long AwayTeamApiId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public int MeetingsCount { get; set; }
    public int HomeTeamWins { get; set; }
    public int AwayTeamWins { get; set; }
    public int Draws { get; set; }
    public IReadOnlyList<HeadToHeadItemDto> RecentMeetings { get; set; } = Array.Empty<HeadToHeadItemDto>();
}
