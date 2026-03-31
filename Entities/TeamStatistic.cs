namespace SmartBets.Entities;

public class TeamStatistic
{
    public long Id { get; set; }
    public long TeamId { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public string Form { get; set; } = string.Empty;
    public int FixturesPlayedTotal { get; set; }
    public int FixturesPlayedHome { get; set; }
    public int FixturesPlayedAway { get; set; }
    public int WinsTotal { get; set; }
    public int WinsHome { get; set; }
    public int WinsAway { get; set; }
    public int DrawsTotal { get; set; }
    public int DrawsHome { get; set; }
    public int DrawsAway { get; set; }
    public int LossesTotal { get; set; }
    public int LossesHome { get; set; }
    public int LossesAway { get; set; }
    public int GoalsForTotal { get; set; }
    public int GoalsForHome { get; set; }
    public int GoalsForAway { get; set; }
    public string? GoalsForAverageTotal { get; set; }
    public string? GoalsForAverageHome { get; set; }
    public string? GoalsForAverageAway { get; set; }
    public int GoalsAgainstTotal { get; set; }
    public int GoalsAgainstHome { get; set; }
    public int GoalsAgainstAway { get; set; }
    public string? GoalsAgainstAverageTotal { get; set; }
    public string? GoalsAgainstAverageHome { get; set; }
    public string? GoalsAgainstAverageAway { get; set; }
    public int CleanSheetsTotal { get; set; }
    public int CleanSheetsHome { get; set; }
    public int CleanSheetsAway { get; set; }
    public int FailedToScoreTotal { get; set; }
    public int FailedToScoreHome { get; set; }
    public int FailedToScoreAway { get; set; }
    public int BiggestStreakWins { get; set; }
    public int BiggestStreakDraws { get; set; }
    public int BiggestStreakLosses { get; set; }
    public string? BiggestWinHome { get; set; }
    public string? BiggestWinAway { get; set; }
    public string? BiggestLossHome { get; set; }
    public string? BiggestLossAway { get; set; }
    public int? BiggestGoalsForHome { get; set; }
    public int? BiggestGoalsForAway { get; set; }
    public int? BiggestGoalsAgainstHome { get; set; }
    public int? BiggestGoalsAgainstAway { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Team Team { get; set; } = null!;
    public League League { get; set; } = null!;
}
