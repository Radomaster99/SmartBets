namespace SmartBets.Dtos;

public class TeamStatisticsSummaryDto
{
    public int Total { get; set; }
    public int Home { get; set; }
    public int Away { get; set; }
}

public class TeamStatisticsAverageDto
{
    public string? Total { get; set; }
    public string? Home { get; set; }
    public string? Away { get; set; }
}

public class TeamStatisticsBiggestDto
{
    public int StreakWins { get; set; }
    public int StreakDraws { get; set; }
    public int StreakLosses { get; set; }
    public string? BiggestWinHome { get; set; }
    public string? BiggestWinAway { get; set; }
    public string? BiggestLossHome { get; set; }
    public string? BiggestLossAway { get; set; }
    public int? BiggestGoalsForHome { get; set; }
    public int? BiggestGoalsForAway { get; set; }
    public int? BiggestGoalsAgainstHome { get; set; }
    public int? BiggestGoalsAgainstAway { get; set; }
}

public class TeamStatisticsDto
{
    public long TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public long LeagueApiId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public string Form { get; set; } = string.Empty;
    public TeamStatisticsSummaryDto FixturesPlayed { get; set; } = new();
    public TeamStatisticsSummaryDto Wins { get; set; } = new();
    public TeamStatisticsSummaryDto Draws { get; set; } = new();
    public TeamStatisticsSummaryDto Losses { get; set; } = new();
    public TeamStatisticsSummaryDto GoalsFor { get; set; } = new();
    public TeamStatisticsAverageDto GoalsForAverage { get; set; } = new();
    public TeamStatisticsSummaryDto GoalsAgainst { get; set; } = new();
    public TeamStatisticsAverageDto GoalsAgainstAverage { get; set; } = new();
    public TeamStatisticsSummaryDto CleanSheets { get; set; } = new();
    public TeamStatisticsSummaryDto FailedToScore { get; set; } = new();
    public TeamStatisticsBiggestDto Biggest { get; set; } = new();
    public DateTime SyncedAtUtc { get; set; }
}
