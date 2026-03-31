namespace SmartBets.Entities;

public class Standing
{
    public long Id { get; set; }

    public long LeagueId { get; set; }
    public int Season { get; set; }
    public long TeamId { get; set; }

    public int Rank { get; set; }
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

    public League League { get; set; } = null!;
    public Team Team { get; set; } = null!;
}