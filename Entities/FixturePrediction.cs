namespace SmartBets.Entities;

public class FixturePrediction
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long? WinnerTeamApiId { get; set; }
    public string? WinnerTeamName { get; set; }
    public string? WinnerComment { get; set; }
    public bool? WinOrDraw { get; set; }
    public string? UnderOver { get; set; }
    public string? Advice { get; set; }
    public string? GoalsHome { get; set; }
    public string? GoalsAway { get; set; }
    public string? PercentHome { get; set; }
    public string? PercentDraw { get; set; }
    public string? PercentAway { get; set; }
    public string? ComparisonFormHome { get; set; }
    public string? ComparisonFormAway { get; set; }
    public string? ComparisonAttackHome { get; set; }
    public string? ComparisonAttackAway { get; set; }
    public string? ComparisonDefenceHome { get; set; }
    public string? ComparisonDefenceAway { get; set; }
    public string? ComparisonPoissonHome { get; set; }
    public string? ComparisonPoissonAway { get; set; }
    public string? ComparisonHeadToHeadHome { get; set; }
    public string? ComparisonHeadToHeadAway { get; set; }
    public string? ComparisonGoalsHome { get; set; }
    public string? ComparisonGoalsAway { get; set; }
    public string? ComparisonTotalHome { get; set; }
    public string? ComparisonTotalAway { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
