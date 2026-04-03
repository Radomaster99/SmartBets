namespace SmartBets.Dtos;

public class StandingDto
{
    public int Rank { get; set; }
    public long TeamId { get; set; }
    public long ApiTeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }

    public int Points { get; set; }
    public int GoalsDiff { get; set; }
    public string? GroupName { get; set; }
    public string? Form { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }

    public int Played { get; set; }
    public int Win { get; set; }
    public int Draw { get; set; }
    public int Lose { get; set; }

    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}
